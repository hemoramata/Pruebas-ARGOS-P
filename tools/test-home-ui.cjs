const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
function fixture(status,body){
 const elements=new Map(),routes=[];
 const get=id=>{if(!elements.has(id))elements.set(id,{hidden:true,textContent:'',addEventListener(){}});return elements.get(id);};
 vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../src/ARGOS.API/wwwroot/home/app.js'),'utf8'),{
 document:{getElementById:get},window:{addEventListener(){}},setInterval(){},location:{replace:url=>routes.push(url)},
 fetch:async()=>({status,ok:status===200,json:async()=>body})});
 return {get,routes,done:()=>new Promise(setImmediate)};
}
test('institution administrator sees configuration without entering setup automatically',async()=>{
 const f=fixture(200,{fullName:'Test',permissions:['personnel.manage']});await f.done();
 assert.equal(f.get('organization').hidden,false);assert.equal(f.get('procedures').hidden,true);assert.deepEqual(f.routes,[]);
});
test('procedure permission only shows procedure card',async()=>{
 const f=fixture(200,{fullName:'Test',permissions:['procedures.manage']});await f.done();
 assert.equal(f.get('organization').hidden,true);assert.equal(f.get('procedures').hidden,false);
});
test('no permissions shows explanation and no privileged links',async()=>{
 const f=fixture(200,{fullName:'<img onerror=bad>',permissions:[]});await f.done();
 assert.equal(f.get('empty').hidden,false);assert.equal(f.get('organization').hidden,true);assert.equal(f.get('procedures').hidden,true);
 assert.equal(f.get('welcome').textContent,'Bienvenido, <img onerror=bad>');
});
test('expired and provisional sessions do not show workspace',async()=>{
 for(const [status,body] of [[401,{}],[200,{mustChangePassword:true}]]){
  const f=fixture(status,body);await f.done();assert.equal(f.get('workspace').hidden,true);assert.match(f.routes[0],/^\/login\//);
 }
});
