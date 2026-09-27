document.querySelectorAll('[data-dialog]').forEach(b=>b.addEventListener('click',()=>document.getElementById(b.dataset.dialog).showModal()));
document.querySelectorAll('[data-close]').forEach(b=>b.addEventListener('click',()=>b.closest('dialog').close()));
const confirmation = document.createElement('dialog');
confirmation.innerHTML = '<div class="dialog-heading"><h2>Confirm this step</h2><button type="button" class="icon-button" aria-label="Close confirmation">×</button></div><p></p><div class="actions"><button type="button" class="button" id="confirm-action">Confirm</button><button type="button" class="button secondary" id="cancel-action">Go back</button></div>';
document.body.appendChild(confirmation);
let pendingForm;
confirmation.querySelector('.icon-button').onclick=()=>confirmation.close();
confirmation.querySelector('#cancel-action').onclick=()=>confirmation.close();
confirmation.querySelector('#confirm-action').onclick=()=>{confirmation.close(); if(pendingForm)HTMLFormElement.prototype.submit.call(pendingForm);};
document.querySelectorAll('form[data-confirm]').forEach(f=>f.addEventListener('submit',e=>{e.preventDefault();pendingForm=f;confirmation.querySelector('p').textContent=f.dataset.confirm;confirmation.showModal();}));
document.querySelectorAll('[data-copy]').forEach(b => b.addEventListener('click', async () => { const original = b.textContent; try { await navigator.clipboard.writeText(b.dataset.copy); b.textContent = 'Copied ✓'; setTimeout(() => b.textContent = original, 1800); } catch { prompt('Copy this invitation:', b.dataset.copy); } }));
document.querySelectorAll('[data-share-native]').forEach(b => { if (!navigator.share) { return; } const row = b.closest('.share-row'); b.hidden = false; b.addEventListener('click', async () => { try { await navigator.share({ title: row.dataset.shareTitle, text: row.dataset.shareText, url: row.dataset.shareUrl }); } catch { } }); });

document.querySelectorAll('dialog[data-open-on-load="true"]').forEach(dialog => dialog.showModal());

document.querySelectorAll('[data-copy-url]').forEach(button=>button.addEventListener('click',async()=>{const url=new URL(button.dataset.copyUrl,location.origin).href;try{await navigator.clipboard.writeText(url);const old=button.textContent;button.textContent='Link copied ✓';setTimeout(()=>button.textContent=old,2000);}catch{prompt('Copy your referral link:',url);}}));
document.querySelectorAll('[data-absolute-input]').forEach(input=>input.value=new URL(input.dataset.absoluteInput,location.origin).href);
document.querySelectorAll('[data-absolute-link]').forEach(el=>el.textContent=new URL(el.dataset.absoluteLink,location.origin).href);
document.querySelectorAll('[data-print]').forEach(button=>button.addEventListener('click',()=>window.print()));

(() => {
 const form=document.getElementById('direct-referral-form');if(!form)return;
 const dialog=form.closest('dialog'), title=document.getElementById('refer-title'), content=document.getElementById('refer-form-content'), success=document.getElementById('refer-confirmation'), errors=document.getElementById('refer-errors');
 const submit=form.querySelector('[type="submit"]'), closeButtons=dialog.querySelectorAll('[data-close]');
 let sending=false;
 const showErrors=messages=>{
  const list=document.createElement('ul');
  messages.forEach(message=>{const item=document.createElement('li');item.textContent=message;list.appendChild(item);});
  errors.replaceChildren(list);errors.classList.remove('validation-summary-valid');errors.classList.add('validation-summary-errors');errors.focus();
 };
 dialog.addEventListener('cancel',event=>{if(sending)event.preventDefault();});
 dialog.addEventListener('close',()=>{if(dialog.dataset.referralComplete==='true')window.location.reload();});
 form.addEventListener('submit',async event=>{
  event.preventDefault();if(sending||dialog.dataset.referralComplete==='true')return;
  sending=true;submit.disabled=true;closeButtons.forEach(button=>button.disabled=true);form.setAttribute('aria-busy','true');
  const label=submit.textContent;submit.textContent='Sending…';errors.replaceChildren();
  errors.classList.remove('validation-summary-errors');errors.classList.add('validation-summary-valid');
  try{
   const response=await fetch(form.action,{method:'POST',body:new FormData(form),credentials:'same-origin',redirect:'error',headers:{'X-Requested-With':'XMLHttpRequest','Accept':'application/json'}});
   if(!response.headers.get('content-type')?.includes('application/json'))throw new Error('Unexpected response');
   const result=await response.json();
   if(response.ok&&result.success===true){
    dialog.dataset.referralComplete='true';content.hidden=true;success.hidden=false;title.textContent=title.dataset.successTitle;dialog.scrollTop=0;
   }else{
    showErrors(Array.isArray(result.errors)&&result.errors.length?result.errors:['Unable to send the invitation. Check your details and try again.']);
   }
  }catch{
   showErrors(['Unable to confirm the invitation. Check your connection and sign-in session, and verify whether the referral was created before trying again.']);
  }finally{
   sending=false;submit.disabled=false;submit.textContent=label;closeButtons.forEach(button=>button.disabled=false);form.removeAttribute('aria-busy');
   if(dialog.dataset.referralComplete==='true')success.querySelector('button').focus();
  }
 });
})();
