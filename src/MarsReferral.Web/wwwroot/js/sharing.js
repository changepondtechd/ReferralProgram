(() => {
 const form=document.getElementById('share-composer'); if(!form)return;
 const customer=document.getElementById('share-customer'), recipient=document.getElementById('share-recipient'), channel=document.getElementById('share-channel'), body=document.getElementById('share-body'), link=document.getElementById('share-link');

 const paintNames=(element,text,names)=>{
  const choices=[...new Set(names.filter(Boolean))].sort((a,b)=>b.length-a.length);
  element.replaceChildren(); if(!choices.length){element.textContent=text;return;}
  const escape=s=>Array.from(s,c=>'\\^$.*+?()[]{}|'.includes(c)?'\\'+c:c).join('');
  const pattern=new RegExp(choices.map(escape).join('|'),'giu');let offset=0;
  for(const match of text.matchAll(pattern)){
   element.append(document.createTextNode(text.slice(offset,match.index)));
   const strong=document.createElement('strong');strong.textContent=match[0];element.append(strong);offset=match.index+match[0].length;
  }
  element.append(document.createTextNode(text.slice(offset)));
 };

 const contact=document.getElementById('share-contact'), contactLabel=document.getElementById('share-contact-label'), contactHelp=document.getElementById('share-contact-help');
 const contactError=()=>{
  const value=contact.value.trim();if(!value)return 'Enter the recipient contact for the selected channel.';
  if(value.length>250||/[\u0000-\u001f\u007f]/.test(value))return 'Enter a valid recipient contact.';
  if(channel.value==='WhatsApp'){
   const digits=value.replace(/[^0-9]/g,'');
   return value.length<=30 && /^\+?[0-9 ()\-.]+$/.test(value) && /^[1-9][0-9]{9,14}$/.test(digits)?'':'Enter a WhatsApp number with country code (10 to 15 digits).';
  }
  let id=value.replace(/^@+/,'');
  if(value.includes(':')){
   try{const u=new URL(value);if(u.protocol!=='https:'||!['linkedin.com','www.linkedin.com'].includes(u.hostname)||u.username||u.password||u.port||!u.pathname.startsWith('/in/'))return 'Paste a LinkedIn HTTPS /in/ profile link.';id=u.pathname.slice(4).replace(/\/+$/,'');}catch{return 'Enter a LinkedIn profile ID or profile link.';}
  }
  return /^(?=[A-Za-z0-9_-]*[A-Za-z])[A-Za-z0-9][A-Za-z0-9_-]{2,99}$/.test(id)?'':'Enter a valid LinkedIn profile ID or /in/ profile link.';
 };
 const updateContact=()=>{
  const linkedin=channel.value==='LinkedIn';
  contactLabel.textContent=linkedin?'LinkedIn ID or profile link':'WhatsApp number';
  contact.type=linkedin?'text':'tel';contact.inputMode=linkedin?'text':'tel';contact.maxLength=linkedin?250:30;
  contact.placeholder=linkedin?'sophie-campbell or https://www.linkedin.com/in/sophie-campbell':'+1 416 555 0123';
  contactHelp.textContent=linkedin?'Paste a LinkedIn profile ID or an HTTPS /in/ profile link.':'Include the country code. Use 10 to 15 digits.';
  contact.setCustomValidity(contactError());document.getElementById('preview-contact').textContent=contact.value.trim() || (linkedin?'LinkedIn profile not entered':'WhatsApp number not entered');
 };
 channel.addEventListener('change',()=>{contact.value='';updateContact();});
 const render=()=>{updateContact();const option=customer instanceof HTMLSelectElement?customer.selectedOptions[0]:customer; const url=new URL('/referral',window.location.origin);url.searchParams.set('code',option?.dataset.code||'');link.value=url.href;document.getElementById('preview-sender').textContent=option?.dataset.name||'Choose a customer';document.getElementById('preview-recipient').textContent=recipient.value;paintNames(document.getElementById('preview-body'),body.value,[option?.dataset.name,recipient.value]);document.getElementById('preview-link').textContent=url.href;document.getElementById('preview-channel').textContent=channel.value+' · Demo';document.getElementById('message-preview').classList.toggle('linkedin',channel.value==='LinkedIn');};
 form.addEventListener('input',render);render();
})();
