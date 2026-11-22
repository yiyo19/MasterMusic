import { spawn, execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';
import { performance } from 'node:perf_hooks';

const catalogo = 'https://localhost:7052/api/v1.0';
const pedidos = 'https://localhost:7267/api/v1.0';
const limiteMs = 2000;
const pausa = ms => new Promise(resolve => setTimeout(resolve, Math.max(0, ms)));
const fecha = () => new Date().toISOString();
const guardar = (file, obj) => fs.writeFileSync(file, JSON.stringify(obj, null, 2)+'\n', 'utf8');

export function parsearCurl(stdout, exitCode) {
  const m = /\n__MM_META__(\d{3});([\d.]+)\s*$/.exec(stdout);
  let body=null;
  try { body=JSON.parse(m ? stdout.slice(0,m.index) : ''); } catch {}
  return { status:m?Number(m[1]):0, ms:m?Number(m[2])*1000:null, body,
    error:exitCode===0?null:`CURL_${exitCode}` };
}

export function resumenRendimiento(rows, count) {
  const tiempos=rows.filter(r=>Number.isFinite(r.ms)).map(r=>r.ms).sort((a,b)=>a-b);
  const aprobadas=rows.filter(r=>r.correcta && r.ms<=limiteMs).length;
  return { requisito:'RNF-04', instrumentos:count, consultas:rows.length,
    correctasEnDosSegundos:aprobadas, porcentaje:100*aprobadas/100,
    p95Ms:tiempos.length===100?tiempos[94]:null,
    cumple:count===100 && rows.length===100 && aprobadas>=95 };
}

export function resumenDisponibilidad(rows, duration, sinReinicios) {
  const porApi=Object.fromEntries(['catalogo','pedidos'].map(api=>{
    const x=rows.filter(r=>r.api===api);
    return [api,{consultas:x.length,correctas:x.filter(r=>r.correcta).length,
      cumpleLecturas:x.length===60 && x.every(r=>r.correcta)}];
  }));
  const puntual=rows.length===120 && rows.every(r=>r.retrasoSegundos<=5);
  const lecturas=Object.values(porApi).every(x=>x.cumpleLecturas);
  return {requisito:'RNF-05',duracionSegundos:duration,porApi,
    intervalosDeTreintaSegundos:puntual,sinReinicios,
    cumple:lecturas && puntual && duration>=1800 && sinReinicios===true};
}

export function esTemporal(i,state) {
  return Number.isInteger(i?.idInstrumento) && i.idInstrumento>0 &&
    !state.originales.includes(i.idInstrumento) &&
    i.nombre?.startsWith(state.prefijo) && i.descripcion===state.marcaTemporal && i.stock===0;
}

export async function limpiarTemporales(state, request) {
  const r=await request(catalogo+'/admin/Instrumentos');
  if(r.status!==200 || !Array.isArray(r.body))throw new Error('No se pudo consultar el catálogo administrativo para limpiar.');
  const resultados=[];
  for(const i of r.body.filter(i=>esTemporal(i,state))) {
    // Se revalida justo antes de borrar y la API también rechaza stock o referencias.
    const actual=await request(catalogo+'/admin/Instrumentos/'+i.idInstrumento);
    if(actual.status===404)continue;
    if(actual.status!==200 || !esTemporal(actual.body,state)) {
      resultados.push({id:i.idInstrumento,eliminado:false,motivo:'El instrumento cambió'});continue;
    }
    const resultado=await request(catalogo+'/Instrumentos/'+i.idInstrumento,'DELETE');
    resultados.push({id:i.idInstrumento,eliminado:resultado.status===204 || resultado.status===404,status:resultado.status});
  }
  return resultados;
}

function curl(url, method='GET', body, token) {
  const u=new URL(url);
  if(u.protocol!=='https:' || u.hostname!=='localhost' || !['7052','7267'].includes(u.port))throw new Error('Solo se admiten las APIs HTTPS locales de MasterMusic.');
  const config=['header = '+JSON.stringify('Accept: application/json')];
  if(token)config.push('header = '+JSON.stringify('Authorization: Bearer '+token));
  if(body!==undefined)config.push('header = '+JSON.stringify('Content-Type: application/json; charset=utf-8'),'data = '+JSON.stringify(JSON.stringify(body)));
  return new Promise(resolve=>{
    const p=spawn('curl.exe',['--disable','--silent','--show-error','--connect-timeout','5','--max-time','10',
      '--request',method,'--url',url,'--write-out','\n__MM_META__%{http_code};%{time_total}','--config','-'],
      {windowsHide:true,stdio:['pipe','pipe','pipe']});
    let stdout='',codeError=false;
    p.stdout.on('data',d=>{stdout+=d.toString('utf8');if(stdout.length>16*1024*1024)p.kill();});
    // No se imprimen cuerpos HTTP, cabeceras, JWT ni mensajes internos del servidor.
    p.stderr.resume();p.stdin.on('error',()=>{});
    p.on('error',()=>{codeError=true;resolve(parsearCurl('',127));});
    p.on('close',code=>{if(!codeError)resolve(parsearCurl(stdout,code??1));});
    p.stdin.end(config.join('\n')+'\n','utf8');
  });
}

function procesosApi() {
  try {
    const command="@(Get-Process -Name 'MasterMusic.Api','MasterMusic.Ventas.Api' -ErrorAction SilentlyContinue | ForEach-Object { [pscustomobject]@{ nombre=$_.ProcessName; id=$_.Id; inicio=$_.StartTime.ToUniversalTime().ToString('o') } }) | ConvertTo-Json -Compress";
    const raw=execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-Command',command],{windowsHide:true,encoding:'utf8',timeout:10000});
    const value=raw.trim()?JSON.parse(raw):[];
    return (Array.isArray(value)?value:[value]).sort((a,b)=>a.nombre.localeCompare(b.nombre)||a.id-b.id);
  } catch {return [];}
}

function version(exe,args) {
  try {return execFileSync(exe,args,{windowsHide:true,encoding:'utf8',timeout:10000}).trim().split(/\r?\n/)[0];}
  catch {return 'No disponible';}
}

function catalogoValido(r) {
  return !r.error && r.status===200 && Array.isArray(r.body) && r.body.every(i=>
    Number.isInteger(i.idInstrumento) && i.idInstrumento>0 && typeof i.nombre==='string' &&
    i.activo===true && Number.isInteger(i.precio) && i.precio>0 && Number.isInteger(i.stock) && i.stock>=0);
}
function pedidosValidos(r) {
  return !r.error && r.status===200 && Array.isArray(r.body) && r.body.every(p=>
    typeof p.idPedido==='string' && ['Pendiente','Confirmado','Cancelado'].includes(p.estado));
}

function informe(run, results) {
  const perf=results.rendimiento,disp=results.disponibilidad;
  return `# Mediciones de cierre de MasterMusic\n\n`+
    `Ejecución: ${run.id}. Inicio UTC: ${run.inicio}. Fin UTC: ${results.fin}. Commit: ${run.equipo.commit}.\n\n`+
    `## Condiciones\n\nAPIs HTTPS locales en 7052 y 7267, con las bases de datos reales. Consultas HTTP secuenciales mediante curl de Windows, con validación del certificado. El tiempo medido es time_total de curl, incluyendo la conexión y la descarga completa. No se reutilizan procesos curl entre consultas.\n\n`+
    `Sistema: ${run.equipo.sistema}. CPU: ${run.equipo.cpu}. RAM: ${run.equipo.ramGB} GB. Node: ${run.equipo.node}. SDK .NET: ${run.equipo.dotnet}. Cliente: ${run.equipo.curl}.\n\n`+
    `Catálogo inicial: ${run.cantidadInicial} instrumentos activos. Para alcanzar exactamente 100 se añadieron instrumentos temporales identificados, con stock cero, sin modificar productos previos. La preparación y limpieza quedan fuera de los tiempos medidos.\n\n`+
    `## RNF 04 rendimiento\n\n${perf?`${perf.correctasEnDosSegundos} de ${perf.consultas} consultas fueron correctas en un máximo de 2000 ms, después de cinco consultas de calentamiento. P95: ${perf.p95Ms??'no disponible'} ms. Resultado: **${perf.cumple?'Cumple':'No cumple'}**.`:'La medición no se completó.'}\n\n`+
    `## RNF 05 disponibilidad\n\n${disp?`Duración: ${disp.duracionSegundos.toFixed(1)} segundos. Catálogo: ${disp.porApi.catalogo.correctas}/${disp.porApi.catalogo.consultas} correctas. Pedidos: ${disp.porApi.pedidos.correctas}/${disp.porApi.pedidos.consultas} correctas. Programación: una consulta por API cada 30 segundos, 60 por API.\n\nSin reinicios según identidad e inicio de procesos al comienzo y final: ${disp.sinReinicios===null?'no verificable automáticamente':disp.sinReinicios?'sí':'no'}. Intervalos con retraso máximo de cinco segundos: ${disp.intervalosDeTreintaSegundos?'sí':'no'}. Resultado: **${disp.cumple?'Cumple':'No acreditado por esta ejecución'}**.`:'La medición no se completó.'}\n\n`+
    `## Limpieza y límites\n\n${results.limpieza?.filter(x=>x.eliminado).length??0} instrumentos temporales eliminados. ${results.limpiezaError??'No se modificaron los instrumentos anteriores ni los pedidos.'}\n\n`+
    `Los CSV contienen las observaciones, también las fallidas. Estas mediciones corresponden a una sola máquina y una ventana local de 30 minutos; no acreditan disponibilidad mensual ni carga concurrente. El reporte no incluye credenciales, JWT ni datos de clientes.\n`;
}

async function main() {
  const repo=process.cwd();
  if(process.argv.includes('--help')) {
    console.log('Desde la raíz MasterMusic: node Documentacion/Pruebas/Medir-RNF-MasterMusic.mjs\nUsa Admin__Email y Admin__Password del entorno. Dura 30 minutos más preparación.\nRecuperar limpieza: node Documentacion/Pruebas/Medir-RNF-MasterMusic.mjs --limpiar RUTA_CARPETA_EJECUCION');return;
  }
  if(!fs.existsSync(path.join(repo,'MasterMusic.slnx')))throw new Error('Ejecuta el comando desde la carpeta raíz de MasterMusic.');
  const email=process.env.Admin__Email, password=process.env.Admin__Password;
  if(!email || !password)throw new Error('Faltan Admin__Email y Admin__Password en esta ventana. Cárgalas desde las variables de usuario siguiendo la guía.');
  const auth=await curl(catalogo+'/Auth/login','POST',{correo:email,clave:password});
  if(auth.status!==200 || auth.body?.usuario?.rol!=='Administrador' || typeof auth.body?.token!=='string')throw new Error(`No se pudo iniciar sesión como Administrador (HTTP ${auth.status}; ${auth.error??'revisa las credenciales'}).`);
  const token=auth.body.token;
  const request=(url,method,body)=>curl(url,method,body,token);
  const outRoot=path.join(repo,'Documentacion','Evidencias','Cierre');
  if(process.argv[2]==='--limpiar') {
    if(!process.argv[3])throw new Error('Indica la carpeta RNF de esta ejecución.');
    const folder=path.resolve(repo,process.argv[3]),relative=path.relative(outRoot,folder);
    if(!relative || relative.startsWith('..') || path.isAbsolute(relative))throw new Error('La carpeta debe estar dentro de Documentacion/Evidencias/Cierre.');
    const state=JSON.parse(fs.readFileSync(path.join(folder,'estado-medicion.json'),'utf8'));
    if(!/^MM-RNF-[0-9a-f-]{36}-/.test(state.prefijo) || !Array.isArray(state.originales))throw new Error('El registro no corresponde a esta herramienta.');
    const clean=await limpiarTemporales(state,request);
    guardar(path.join(folder,'limpieza-recuperada.json'),{fecha:fecha(),resultados:clean});
    console.log(`Limpieza: ${clean.filter(x=>x.eliminado).length} eliminados; ${clean.filter(x=>!x.eliminado).length} conservados para revisar.`);return;
  }
  if(process.argv.length>2)throw new Error('Argumentos no reconocidos. Consulta --help.');
  const claims=JSON.parse(Buffer.from(token.split('.')[1],'base64url').toString('utf8'));
  if(!Number.isFinite(claims.exp) || claims.exp*1000-Date.now()<35*60*1000)throw new Error('El JWT debe tener al menos 35 minutos de vigencia para completar la medición.');
  const inicial=await request(catalogo+'/Instrumentos');
  if(!catalogoValido(inicial))throw new Error(`El catálogo no respondió correctamente (HTTP ${inicial.status}).`);
  if(inicial.body.length<1 || inicial.body.length>100)throw new Error('Se requieren entre 1 y 100 instrumentos activos de partida. No se modificó el catálogo.');
  const preflight=await request(pedidos+'/admin/Pedidos');
  if(!pedidosValidos(preflight))throw new Error(`La API de pedidos no respondió correctamente a la consulta administrativa (HTTP ${preflight.status}).`);
  const originales=await request(catalogo+'/admin/Instrumentos');
  if(originales.status!==200 || !Array.isArray(originales.body))throw new Error('No se pudo registrar el catálogo original.');
  const id=randomUUID(),folder=path.join(outRoot,'RNF-'+fecha().replace(/[:.]/g,'-')+'-'+id.slice(0,8));
  fs.mkdirSync(folder,{recursive:true});
  const state={id,inicio:fecha(),prefijo:'MM-RNF-'+id+'-',marcaTemporal:'Datos temporales de medición RNF '+id,
    originales:originales.body.map(i=>i.idInstrumento),creados:[],cantidadInicial:inicial.body.length,
    equipo:{sistema:os.type()+' '+os.release(),cpu:os.cpus()[0]?.model??'No disponible',ramGB:Math.round(os.totalmem()/1024**3),
      node:process.version,dotnet:version('dotnet.exe',['--version']),curl:version('curl.exe',['--version']),
      commit:version('git.exe',['rev-parse','--short','HEAD'])}};
  guardar(path.join(folder,'estado-medicion.json'),state);
  const perfRows=[],dispRows=[],results={};
  let interrupted=false,clockStart;
  const interrupt=()=>{interrupted=true;console.log('Interrupción solicitada; se guardarán los datos y se intentará limpiar los temporales.');};
  process.once('SIGINT',interrupt);
  const comprobar=()=>{if(interrupted)throw new Error('Medición interrumpida.');};
  try {
    const seed=inicial.body[0];
    for(let i=inicial.body.length;i<100;i++) {
      comprobar();
      const r=await request(catalogo+'/Instrumentos','POST',{nombre:state.prefijo+String(i+1).padStart(3,'0'),
        descripcion:state.marcaTemporal,precio:1000,stock:0,idCategoria:seed.idCategoria,idMarca:seed.idMarca});
      if(r.status!==201 || !esTemporal(r.body,state))throw new Error(`No se pudo preparar un instrumento temporal (HTTP ${r.status}).`);
      state.creados.push(r.body.idInstrumento);guardar(path.join(folder,'estado-medicion.json'),state);
    }
    const listo=await request(catalogo+'/Instrumentos');
    if(!catalogoValido(listo) || listo.body.length!==100)throw new Error('El catálogo no quedó con exactamente 100 instrumentos activos.');
    console.log('Catálogo preparado con 100 instrumentos. Ejecutando cinco consultas de calentamiento.');
    for(let i=0;i<5;i++){comprobar();const r=await request(catalogo+'/Instrumentos');if(!catalogoValido(r)||r.body.length!==100)throw new Error('Falló una consulta de calentamiento.');}
    fs.writeFileSync(path.join(folder,'rendimiento.csv'),'numero,fechaUtc,status,ms,instrumentos,correcta,error\n');
    for(let i=1;i<=100;i++) {
      comprobar();const t=fecha(),r=await request(catalogo+'/Instrumentos');
      const row={numero:i,fechaUtc:t,status:r.status,ms:r.ms,instrumentos:Array.isArray(r.body)?r.body.length:0,
        correcta:catalogoValido(r)&&r.body.length===100,error:r.error??''};
      perfRows.push(row);fs.appendFileSync(path.join(folder,'rendimiento.csv'),Object.values(row).join(',')+'\n');
    }
    results.rendimiento=resumenRendimiento(perfRows,100);
    console.log(`Rendimiento: ${results.rendimiento.correctasEnDosSegundos}/100 correctas en 2 segundos. Iniciando los 30 minutos de disponibilidad.`);
    const pInicio=procesosApi();
    guardar(path.join(folder,'procesos-inicio.json'),pInicio);
    fs.writeFileSync(path.join(folder,'disponibilidad.csv'),'numero,api,fechaUtc,status,ms,correcta,retrasoSegundos,error\n');
    clockStart=performance.now();
    for(let i=0;i<60;i++) {
      while(performance.now()<clockStart+i*30000){comprobar();await pausa(Math.min(1000,clockStart+i*30000-performance.now()));}
      comprobar();
      for(const [api,url,validate] of [['catalogo',catalogo+'/Instrumentos',catalogoValido],['pedidos',pedidos+'/admin/Pedidos',pedidosValidos]]) {
        const retraso=Math.max(0,(performance.now()-(clockStart+i*30000))/1000);
        const t=fecha(),r=await request(url);
        const row={numero:i+1,api,fechaUtc:t,status:r.status,ms:r.ms,correcta:validate(r),retrasoSegundos:Number(retraso.toFixed(3)),error:r.error??''};
        dispRows.push(row);fs.appendFileSync(path.join(folder,'disponibilidad.csv'),Object.values(row).join(',')+'\n');
      }
      if(i%2===0 || i===59)console.log(`Disponibilidad ${i+1}/60: catálogo ${dispRows.at(-2).status}, pedidos ${dispRows.at(-1).status}.`);
    }
    while(performance.now()<clockStart+1800000){comprobar();await pausa(Math.min(1000,clockStart+1800000-performance.now()));}
    const duration=(performance.now()-clockStart)/1000,pFin=procesosApi();
    guardar(path.join(folder,'procesos-fin.json'),pFin);
    const completos=pInicio.some(p=>p.nombre==='MasterMusic.Api')&&pInicio.some(p=>p.nombre==='MasterMusic.Ventas.Api');
    const sinReinicios=completos?JSON.stringify(pInicio)===JSON.stringify(pFin):null;
    results.disponibilidad=resumenDisponibilidad(dispRows,duration,sinReinicios);
  } catch(e) {
    results.error=e.message;
    if(perfRows.length)results.rendimiento=resumenRendimiento(perfRows,100);
    if(clockStart!==undefined)results.disponibilidad=resumenDisponibilidad(dispRows,(performance.now()-clockStart)/1000,null);
    console.error(e.message);process.exitCode=1;
  } finally {
    console.log('Guardando resultados y limpiando únicamente instrumentos temporales...');
    try {results.limpieza=await limpiarTemporales(state,request);}catch {results.limpiezaError='La limpieza no se pudo completar. Usa --limpiar con esta carpeta cuando las APIs respondan.';process.exitCode=1;}
    if(results.limpieza?.some(x=>!x.eliminado)) {results.limpiezaError='Algunos temporales cambiaron o no pudieron eliminarse; se conservaron para revisar.';process.exitCode=1;}
    results.fin=fecha();guardar(path.join(folder,'resultado.json'),results);
    fs.writeFileSync(path.join(folder,'README.md'),informe(state,results),'utf8');
    console.log('Evidencia guardada en: '+folder);
    console.log('RNF-04: '+(results.rendimiento?.cumple?'CUMPLE':'NO ACREDITADO'));
    console.log('RNF-05: '+(results.disponibilidad?.cumple?'CUMPLE':'NO ACREDITADO'));
    if(!results.rendimiento?.cumple || !results.disponibilidad?.cumple)process.exitCode=1;
    process.removeListener('SIGINT',interrupt);
  }
}

if(process.argv[1] && path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  main().catch(e=>{console.error(e.message);process.exitCode=1;});
}
