const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const logic = require('../src/ARGOS.API/wwwroot/procedures/logic.js');
const source = fs.readFileSync(path.join(__dirname,'../src/ARGOS.API/wwwroot/procedures/app.js'),'utf8');
const authority = {positions:[{id:'pos',name:'Puesto'}],assignments:[{id:'assignment',positionId:'pos'}],policies:[{id:'policy',version:1,procedureCode:'P',rules:{requiredApprovalCount:1,authorities:[{positionId:'pos',competenceCode:'C',action:'submit-review'}]}}]};
const draft={id:'id',code:'P',name:'Trámite',version:1,status:'Draft',decisions:[]};
function fixture(responses){
  const elements=new Map(),calls=[];
  function make(){return {value:'',hidden:false,disabled:false,children:[],handlers:{},addEventListener(type,fn){this.handlers[type]=fn;},replaceChildren(){this.children=[];},append(child){this.children.push(child); if(this===elements.get('action')&&this.children.length===1)this.value=child.value;},querySelectorAll(){return this.children;},reset(){},focus(){}};}
  const el=id=>{if(!elements.has(id))elements.set(id,make());return elements.get(id);};
  vm.runInNewContext(source,{ProcedureUI:logic,document:{getElementById:el,createElement:make},window:{addEventListener(){}},location:{protocol:'https:',assign(){}},fetch:async(url,options)=>{calls.push({url,body:options.body?JSON.parse(options.body):undefined});const r=responses.shift();if(!r)throw Error('Unexpected request');return {status:r.status,ok:r.status<400,json:async()=>r.body};}});
  return {el,calls,async settle(){await new Promise(setImmediate);},async submit(id){el(id).handlers.submit({preventDefault(){}});await new Promise(setImmediate);},async select(){el('list').children[0].handlers.click();await new Promise(setImmediate);}};
}
test('Screen freezes exact decision until confirmation and clears OTP on failure',async()=>{
  const f=fixture([{status:200,body:authority},{status:200,body:[draft]},{status:200,body:{id:'challenge',maskedDestination:'***',expiresAt:new Date(Date.now()+60000).toISOString()}},{status:403}]);
  await f.settle();await f.select();f.el('comments').value='Original';await f.submit('decision');
  assert.equal(f.el('decision-fields').disabled,true);assert.equal(f.el('confirmation').hidden,false);
  f.el('comments').value='Changed programmatically';f.el('code').value='123456';await f.submit('confirmation');
  assert.equal(f.calls[3].body.decision.comments,'Original');assert.equal(f.calls[3].body.decision.policyId,'policy');
  assert.equal(f.el('code').value,'');assert.equal(f.el('confirmation').hidden,true);
  assert.match(f.el('message').textContent,/No se autorizó/);
});
test('Expired session hides data and offers login',async()=>{
  const f=fixture([{status:401}]);await f.settle();assert.equal(f.el('workspace').hidden,true);assert.equal(f.el('login-link').hidden,false);
});
test('Cancel never submits the pending decision',async()=>{
  const f=fixture([{status:200,body:authority},{status:200,body:[draft]},{status:200,body:{id:'challenge',expiresAt:new Date(Date.now()+60000).toISOString()}}]);
  await f.settle();await f.select();await f.submit('decision');f.el('code').value='123456';f.el('cancel').handlers.click();
  assert.equal(f.calls.length,3);assert.equal(f.el('code').value,'');assert.equal(f.el('decision-fields').disabled,false);
});
test('New version preserves old identity and opens the returned draft',async()=>{
  const published={...draft,status:'Published'};
  const revision={...draft,id:'new-id',version:2};
  const f=fixture([{status:200,body:authority},{status:200,body:[published]},{status:201,body:revision},{status:200,body:authority},{status:200,body:[published,revision]}]);
  await f.settle();await f.select();assert.equal(f.el('revision-panel').hidden,false);
  f.el('revision-name').value='Revisión';f.el('revision-description').value='Cambios';await f.submit('revision');
  assert.equal(f.calls[2].url,'/api/procedures/id/versions');assert.equal(f.calls[2].body.name,'Revisión');
  assert.match(f.el('state').textContent,/Versión 2/);assert.equal(f.el('revision-panel').hidden,true);
});
