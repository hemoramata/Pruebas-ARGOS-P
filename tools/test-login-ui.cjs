const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../src/ARGOS.API/wwwroot/login/app.js'), 'utf8');
function fixture(responses) {
  const elements = new Map();
  const calls = [];
  const navigation = [];
  function element(id) {
    if (!elements.has(id)) elements.set(id, {value:'', hidden:false, handlers:{}, focus(){}, addEventListener(event, handler){this.handlers[event]=handler;}});
    return elements.get(id);
  }
  vm.runInNewContext(source, {
    document:{getElementById:element, querySelectorAll:()=>[]},
    location:{protocol:'https:', assign:url=>navigation.push(url)},
    fetch:async (url, options)=>{calls.push({url, body:JSON.parse(options.body)}); const r=responses.shift(); if(!r) throw Error('Unexpected request'); return {ok:r.status<400, status:r.status, json:async()=>r.body};}
  });
  return {element,calls,navigation, async submit(id){element(id).handlers.submit({preventDefault(){}}); await new Promise(setImmediate);}};
}
test('MFA success clears credentials and opens home for authorized administrators', async()=>{
  const f=fixture([{status:200,body:{id:'id',binding:'binding',maskedDestination:'***12'}},{status:200,body:{permissions:['personnel.manage'],mustChangePassword:false}}]);
  f.element('identification').value='test'; f.element('password').value='secret';
  await f.submit('login');
  assert.equal(f.element('password').value,''); assert.equal(f.element('verify').hidden,false);
  f.element('code').value='123456'; await f.submit('verify');
  assert.equal(f.element('code').value,''); assert.deepEqual(f.navigation,['/home/']);
  assert.equal(f.calls[1].body.binding,'binding');
});
test('Invalid login clears the password and reports the failure', async()=>{
  const f=fixture([{status:401,body:{}}]); f.element('password').value='secret';
  await f.submit('login'); assert.equal(f.element('password').value,'');
  assert.match(f.element('message').textContent,/No se pudo verificar/); assert.deepEqual(f.navigation,[]);
});

test('Recovery sends only identification and clears new passwords after reset', async()=>{
  const f=fixture([{status:200,body:{id:'recovery',binding:'bound'}},{status:204}]);
  f.element('recovery-identification').value='001'; await f.submit('recover');
  assert.deepEqual(f.calls[0].body,{identification:'001'});
  assert.equal(f.element('reset').hidden,false);
  f.element('recovery-code').value='123456'; f.element('recovery-password').value='New-password-2'; f.element('recovery-repeat').value='New-password-2';
  await f.submit('reset'); assert.equal(f.element('recovery-code').value,''); assert.equal(f.element('recovery-password').value,'');
  assert.equal(f.element('login').hidden,false); assert.deepEqual(f.navigation,[]);
});
test('Procedure managers reach home after login',async()=>{
  const f=fixture([{status:200,body:{id:'i',binding:'b'}},{status:200,body:{permissions:['procedures.manage'],mustChangePassword:false}}]);
  await f.submit('login'); await f.submit('verify'); assert.deepEqual(f.navigation,['/home/']);
});
test('Required password change stays in the dedicated form', async()=>{
  const f=fixture([{status:200,body:{id:'id',binding:'b'}},{status:200,body:{permissions:[],mustChangePassword:true}}]);
  await f.submit('login'); await f.submit('verify');
  assert.equal(f.element('change').hidden,false); assert.deepEqual(f.navigation,[]);
  f.element('new').value='one'; f.element('repeat').value='two'; await f.submit('change');
  assert.equal(f.calls.length,2); assert.match(f.element('message').textContent,/no coinciden/);
});
