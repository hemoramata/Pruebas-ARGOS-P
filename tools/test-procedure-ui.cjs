const test = require('node:test');
const assert = require('node:assert/strict');
const ui = require('../src/ARGOS.API/wwwroot/procedures/logic.js');
const policy = {id:'policy',procedureCode:'P',version:1,rules:{emergencyPublicationAllowed:false,authorities:[{action:'submit-review',positionId:'pos',competenceCode:'C'}]}};
const authority = {policies:[policy],assignments:[{id:'assignment',positionId:'pos'}]};
const draft = {id:'procedure',code:'P',status:'Draft'};
test('No policy or wrong assignment grants no actions',()=>{
  assert.equal(ui.options(draft,{...authority,policies:[]}).length,0);
  assert.equal(ui.options(draft,{...authority,assignments:[{id:'other',positionId:'other'}]}).length,0);
  assert.equal(ui.options(draft,authority).length,1);
});
test('Reviewed procedure stays bound to its frozen policy',()=>{
  const newest = {...policy,id:'new',version:2};
  assert.equal(ui.policyFor(draft,[policy,newest]).id,'new');
  assert.equal(ui.policyFor({...draft,approvalPolicyVersion:'policy'},[policy,newest]).id,'policy');
  assert.equal(ui.policyFor({...draft,approvalPolicyVersion:'missing'},[policy,newest]),undefined);
});
test('Snapshot fixes payload and confirmation never sets client approval rules',()=>{
  const snapshot = ui.prepare(draft.id,ui.options(draft,authority)[0],'Review','');
  const challenge = {id:'challenge',expiresAt:new Date(Date.now()+60000).toISOString()};
  const result = ui.confirmation(snapshot,challenge,'123456');
  assert.equal(result.decision.challengeId,'challenge');
  assert.equal(result.decision.comments,'Review');
  assert.equal(result.policy,undefined); assert.equal(snapshot.request.decision.code,undefined);
  assert.equal(result.decision.actorReference,undefined); assert.equal(result.decision.positionReference,undefined);
});
test('Expired or malformed codes cannot be submitted',()=>{
  const snapshot = ui.prepare('p',ui.options(draft,authority)[0],'','');
  assert.throws(()=>ui.confirmation(snapshot,{expiresAt:new Date(0).toISOString()},'123456'),/venció/);
  assert.throws(()=>ui.confirmation(snapshot,{expiresAt:new Date(Date.now()+60000).toISOString()},'abc'),/seis/);
});
test('Emergency requires a future deadline and preserves it',()=>{
  const option = {...ui.options(draft,authority)[0],action:'publish-emergency'};
  assert.throws(()=>ui.prepare('p',option,'',''),/futura/);
  const snapshot = ui.prepare('p',option,'',new Date(Date.now()+3600000).toISOString());
  assert.equal(ui.confirmation(snapshot,{id:'c',expiresAt:new Date(Date.now()+60000).toISOString()},'123456').ratificationDueAtUtc,snapshot.request.ratificationDueAtUtc);
});
