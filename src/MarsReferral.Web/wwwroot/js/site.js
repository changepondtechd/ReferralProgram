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
