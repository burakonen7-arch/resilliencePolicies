import json,time,urllib.request,urllib.error,concurrent.futures
base='http://127.0.0.1:5080'
checks=0
def request(path,method='GET',data=None):
    req=urllib.request.Request(base+path,data=json.dumps(data).encode() if data is not None else None,method=method,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=15) as r: return r.status,json.loads(r.read() or 'null')
    except urllib.error.HTTPError as e: return e.code,json.loads(e.read() or 'null')
def check(condition,label):
    global checks
    assert condition,label
    checks+=1
    print('PASS',label)
payload={'name':'Smoke product','price':12.50,'stock':3}
status,p=request('/api/products','POST',payload); check(status==201,'create')
try:
    path='/api/products/'+p['id']
    check(request(path)[1]['name']==payload['name'],'read')
    check(request(path,'PUT',dict(payload,stock=5))[0]==204,'update')
    check(request(path)[1]['stock']==5,'persist update')
    check(request('/api/products','POST',dict(payload,price=-1))[0]==400,'validation')
    status,r=request('/api/resilience/retry?failures=2'); check(status==200 and r['attempts']==3,'retry recovers')
    check(request('/api/resilience/retry?failures=10')[0]==503,'retry exhausted')
    status,r=request('/api/resilience/fallback?failures=1'); check(status==200 and r['fallback'] and r['products']==[],'fallback')
    check(request('/api/resilience/timeout?delayMs=1500')[0]==504,'timeout')
    status,r=request('/api/resilience/hedging?failures=1'); check(status==200 and r['attempts']>=2,'hedging')
    check(request('/api/resilience/cache')[1]['attempts']==1,'cache miss')
    check(request('/api/resilience/cache?failures=10')[1]['attempts']==0,'cache hit')
    for _ in range(3): check(request('/api/resilience/rate-limit')[0]==200,'rate permit')
    check(request('/api/resilience/rate-limit')[0]==429,'rate rejected')
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        statuses=list(pool.map(lambda _:request('/api/resilience/bulkhead?delayMs=500')[0],range(3)))
    check(sorted(statuses)==[200,200,429],'bulkhead concurrent rejection')
    for _ in range(4): check(request('/api/resilience/circuit-breaker?failures=1')[0]==503,'breaker failure')
    check('Circuit is open' in request('/api/resilience/circuit-breaker')[1]['title'],'breaker opens')
    time.sleep(5.2)
    check(request('/api/resilience/circuit-breaker')[0]==200,'breaker recovers')
    status,r=request('/api/resilience/combined?failures=2'); check(status==200 and r['attempts']==3,'combined recovers')
    check(request('/api/resilience/unknown')[0]==400,'unknown policy')
finally:
    check(request('/api/products/'+p['id'],'DELETE')[0]==204,'delete')
    check(request('/api/products/'+p['id'])[0]==404,'deleted product missing')
print(f'{checks} checks passed')
