from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor
from reportlab.lib.utils import simpleSplit
import pypdfium2 as pdfium

root = Path(__file__).resolve().parents[1]
target = root / 'src/MarsReferral.Web/wwwroot/guides/mars-referral-guide.pdf'
target.parent.mkdir(parents=True, exist_ok=True)
c = canvas.Canvas(str(target), pagesize=(595,842))
c.setTitle('MARS Circle Referral Guide')
c.setAuthor('Mars Team')
c.setFillColor(HexColor('#f6f8f2')); c.rect(0,0,595,842,fill=1,stroke=0)
c.setFillColor(HexColor('#234f40')); c.rect(0,625,595,217,fill=1,stroke=0)
c.setFillColor(HexColor('#d4eb8c')); c.setFont('Helvetica-Bold',10); c.drawString(45,792,'MARS CIRCLE  /  YOUR REFERRAL GUIDE')
c.setFillColor(HexColor('#ffffff')); c.setFont('Helvetica-Bold',31); c.drawString(45,743,'A friendly introduction.'); c.drawString(45,703,'A shared reward.')
c.setFont('Helvetica',11); c.drawString(45,659,'Friend: 500 welcome credits   |   You: 1,000 referral credits')
steps=[('Sign in','Open your customer workspace. Find your personal sharing code on the overview.'),('Share your invitation','Copy your code or invitation link and share it with a friend.'),('Your friend attaches the code','Your friend registers or signs in, then attaches your code if their profile has no referral code.'),('Reach qualifying funding','After code attachment, an application reaches confirmed account funding. Operations reviews both rewards.'),('Track your benefit','Open Rewards for status and payment details. Check Inbox for funding invitations and reward updates.')]
y=581
for i,(title,body) in enumerate(steps,1):
    c.setFillColor(HexColor('#89a76c')); c.setFont('Helvetica-Bold',23); c.drawString(45,y,str(i).zfill(2))
    c.setFillColor(HexColor('#234f40')); c.setFont('Helvetica-Bold',14); c.drawString(90,y+2,title)
    c.setFont('Helvetica',10); c.setFillColor(HexColor('#697e5b'))
    for j,line in enumerate(simpleSplit(body,'Helvetica',10,450)): c.drawString(90,y-18-j*15,line)
    y-=82
c.setFillColor(HexColor('#234f40'));c.setFont('Helvetica-Bold',11);c.drawString(45,156,'Before you share')
c.setFont('Helvetica',9)
terms='One code can serve many friends. Each profile can attach only one code, with no replacements or self-referrals. The first funding after attachment qualifies once per referred customer; earlier funding is not rewarded retroactively. Credits, funding, messages and payouts are simulated. No real money or email is sent.'
for j,line in enumerate(simpleSplit(terms,'Helvetica',9,505)):c.drawString(45,136-j*13,line)
c.setStrokeColor(HexColor('#dce6d3'));c.line(45,58,550,58)
c.setFont('Helvetica',8);c.drawString(45,40,'Mars Team | Demo guide | Open Referral guide in the portal for details');c.drawRightString(550,40,'1 / 1')
c.save()
out=root/'artifacts';out.mkdir(exist_ok=True)
pdf=pdfium.PdfDocument(str(target));pdf[0].render(scale=1.3).to_pil().save(out/'guide-preview.png')
print(target)
