"""Integration checks for three independent portals. Uses disposable demo data."""
import html
import http.cookiejar
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

base = sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:5080'
checks = 0
def check(ok, name):
    global checks
    assert ok, name
    checks += 1
    print('PASS', name)
class Client:
    def __init__(self):
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.body = ''
    def request(self, path, data=None):
        payload = None if data is None else urllib.parse.urlencode(data).encode()
        try: response = self.opener.open(base + path, payload)
        except urllib.error.HTTPError as ex: response = ex
        self.body = response.read().decode('utf-8', errors='replace')
        return response.status
    def post(self, path, data):
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', self.body)
        assert token, 'CSRF token missing'
        return self.request(path, {**data, '__RequestVerificationToken': html.unescape(token[1])})

guest=Client()
check(guest.request('/')==200 and 'The right space.' in guest.body, 'Portal selector renders three dedicated entry points')
check(guest.request('/admin')==200 and 'ADMIN ACCESS' in guest.body, 'Admin has its own sign-in')
check(guest.request('/customer')==200 and 'CUSTOMER ACCESS' in guest.body, 'Customer has its own sign-in')
check(guest.request('/join')==200 and 'CREATE YOUR ACCOUNT' in guest.body, 'Registration is in the onboarding portal')
check(guest.request('/admin/login', {})==400, 'Missing CSRF token rejected')
user=Client();user.request('/join/register')
name='Portal Test '+str(time.time_ns())[-9:];email=name.replace(' ','').lower()+'@example.com'
check(user.post('/join/register',{'Name':name,'Email':email,'ReferralCode':'MARS000001'})==200 and 'Your introduction' in user.body, 'Registration enters onboarding instead of customer portal')
check(user.request('/customer')==200 and 'CUSTOMER ACCESS' in user.body, 'Onboarding cookie does not authenticate customer portal')
user.request('/join/welcome')
check(user.post('/join/complete',{'understood':'false'})==200 and 'Confirm that you understand' in user.body, 'Onboarding acknowledgment validated on server')
check(user.post('/join/complete',{'understood':'true'})==200 and 'Onboarding complete' in user.body, 'Completing onboarding opens customer portal')
check('Referral code already used' in user.body, 'Registration referral remains attached')
user.request('/customer/login')
customer_id=re.search(r'<option value="(\d+)">'+re.escape(name),user.body)[1]
user.request('/customer')
check(user.post('/customer/referral/attach',{'code':'MARS000006'})==200 and 'already used' in user.body, 'Replacing saved referral is rejected')
check(user.request('/admin/customers')==200 and 'ADMIN ACCESS' in user.body, 'Customer cookie does not authenticate admin portal')
check(user.request('/customer/customers')==404, 'Customer portal has no admin customer directory endpoint')
user.request('/customer/applications')
check(user.post('/customer/applications/create',{'purpose':'Refinance','amount':'abc'})==200 and 'submitted values are invalid' in user.body, 'Malformed numeric amount returns validation')
check(user.post('/customer/applications/create',{'customerId':'1','purpose':'Refinance','amount':'200000'})==200 and 'Saved successfully' in user.body, 'Customer application uses signed-in identity, ignoring spoofed customer ID')
app_id=re.search(r'<strong>#(\d+)</strong>',user.body)[1]
check(name in user.body and 'Rahul Sharma' not in user.body, 'Only own applications appear in customer portal')
check(user.request('/customer/applications/approve',{'id':app_id,'amount':'100000'})==404, 'Customer portal exposes no amount approval endpoint')
admin=Client();admin.request('/admin/login')
check(admin.post('/admin/login',{})==200 and 'ADMIN / PROGRAM AT A GLANCE' in admin.body, 'Admin sign-in enters independent admin dashboard')
check(admin.request('/customer')==200 and 'CUSTOMER ACCESS' in admin.body, 'Admin session alone does not select an arbitrary customer')
for route in ['customers','applications','rewards','inbox','guide','activity']:
    check(admin.request('/admin/'+route)==200, 'Admin '+route+' page renders')
admin.request('/admin/applications')
check(admin.post('/admin/applications/approve',{'id':app_id,'amount':'200001'})==200 and 'cannot exceed' in admin.body, 'Cannot approve more than requested')
check(admin.post('/admin/applications/approve',{'id':app_id,'amount':'180000'})==200 and 'Saved successfully' in admin.body, 'Admin approves reduced amount')
row=next(row for row in re.findall(r'<tr>(.*?)</tr>',admin.body,re.S) if '#'+app_id in row)
check('1,80,000.00' in row or '180,000.00' in row, 'Approved amount displayed separately')
check(admin.post('/admin/applications/transition',{'id':app_id,'stage':'Funded'})==200 and 'Saved successfully' in admin.body, 'Funding confirmation succeeds after amount approval')
check(admin.post('/admin/applications/transition',{'id':app_id,'stage':'Funded'})==200 and 'already processed' in admin.body, 'Duplicate funding blocked')
admin.request('/admin/rewards')
rows=re.findall(r'<tr>(.*?)</tr>',admin.body,re.S)
reward_ids=[re.search(r'#(\d+) ·',row)[1] for row in rows if 'Application #'+app_id in row]
check(len(reward_ids)==2, 'Funding creates exactly one reward pair')
rid=reward_ids[0]
check(admin.post('/admin/rewards/review',{'id':rid,'approve':'true'})==200 and 'Saved successfully' in admin.body, 'Admin approves reward')
reference='PORTAL-'+str(time.time_ns())
check(admin.post('/admin/rewards/pay',{'id':rid,'reference':reference,'successful':'false'})==200 and 'Demo payment failed' in admin.body, 'Failed payout remains retryable')
check(admin.post('/admin/rewards/pay',{'id':rid,'reference':reference,'successful':'true'})==200 and reference in admin.body, 'Confirmed payout succeeds')
check(admin.post('/admin/rewards/pay',{'id':rid,'reference':reference,'successful':'true'})==200 and 'cannot be paid' in admin.body, 'Duplicate payout blocked')
admin.request('/admin/customers')
check(admin.post('/admin/shadow/start',{'customerId':customer_id})==200 and 'Viewing as '+name in admin.body, 'Admin can shadow a selected customer')
check('Admin shadow session' in admin.body and 'Return to Admin' in admin.body, 'Shadow identity and return action are explicit')
check(admin.post('/customer/applications/create',{'purpose':'Refinance','amount':'10000'})==200 and 'Saved successfully' in admin.body, 'Admin shadow can use customer functionality')
admin.request('/admin/activity')
check('Admin shadow of customer #'+customer_id in admin.body, 'Shadow actions attributed to admin in audit')
admin.request('/customer')
check(admin.post('/customer/shadow/return',{})==200 and 'Customer directory' in admin.body, 'Return to admin preserves admin authentication')
check(admin.request('/customer')==200 and 'CUSTOMER ACCESS' in admin.body, 'Returning to admin clears shadow cookie')
admin.request('/admin/customers')
admin.post('/admin/shadow/start',{'customerId':'3'})
admin.request('/admin')
check(admin.post('/admin/logout',{})==200 and 'ADMIN ACCESS' in admin.body, 'Admin sign-out succeeds')
check(admin.request('/customer')==200 and 'CUSTOMER ACCESS' in admin.body, 'Admin sign-out also invalidates shadow session')
user.request('/customer/inbox')
check('Your funding is complete' in user.body and 'Your simple referral guide' in user.body, 'Customer gets funding invitation and guide')
check(user.request('/guides/mars-referral-guide.pdf')==200 and user.body.startswith('%PDF'), 'PDF guide still downloads')
check(user.request('/Account/Login',{'mode':'operations'})==405, 'Legacy mixed-role login cannot grant admin access')
print(f'\n{checks} portal HTTP integration checks passed.')
