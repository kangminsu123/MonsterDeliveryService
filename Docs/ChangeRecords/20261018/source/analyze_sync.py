from pathlib import Path
import csv,json,statistics,math,re
R=Path(r'C:/Users/xlfhf/Documents/Codex/2026-09-18/https-github-com-kangminsu123-monsterdeliveryservice-git/MonsterDeliveryService')
D=R/'Docs/ChangeRecords/20261018';E=D/'evidence'
result={}
for label in ['before','after']:
 result[label]={'benchmark':json.loads((E/f'sync-benchmark-{label}.json').read_text())}
 for role in ['host','client']:
  rows=list(csv.DictReader((E/f'sync-connected-{label}-{role}.csv').open()))
  selected=[r for r in rows if 3<=float(r['time'])<=6.5]
  stats={}
  for key in ['frame_ms','ping_ms','ack_ms','receive_hz','work_ms','send_kb','receive_kb']:
   a=sorted(float(r[key]) for r in selected);stats[key]={'mean':statistics.mean(a),'p95':a[int((len(a)-1)*.95)]}
  stats['measurement_window']='3.0 to6.5s before equipment changes'
  proof=json.loads((E/f'sync-perf/{"host" if role=="host" else "client-1"}-{label}-loss.json').read_text())
  stats['seven_second_movement_proof']=proof
  result[label][role]=stats
  text=(E/f'sync-connected-{label}-{role}.txt').read_text(encoding='utf-8');assert len(re.findall('^PASS ',text,re.M))==14 and not re.findall('^FAIL ',text,re.M)
for name in ['server_capture','unchanged_parcels','apply_frame','serialize_compress']:
 before=result['before']['benchmark'][name+'_ms_per_call'];after=result['after']['benchmark'][name+'_ms_per_call']
 result.setdefault('cpu_change_percent',{})[name]=(after/before-1)*100
result['measurement_limits']=['Two real Editor processes on same PC; no physical WAN.','60ms delayed application frame/input messages each way, every fifth omitted; transport ping excludes this artificial application queue.','Per-thread GC allocation counter returns0 on this runtime; reported as unavailable, not zero allocations.','One warmed benchmark and one 34s delayed run per version; percentages are descriptive samples, not repeated confidence intervals.','Traffic rate may increase because30Hz schedule drift is corrected; packet size and bytes/s are different metrics.','Render model columns in this first audit were sampled before LateUpdate; do not use them to claim final rendered smoothing.']
result['measurement_limits'].append('The before/after connected CSV ping fields predate final server-ID RTT lookup correction; do not use their ping values as an improvement comparison. Final movement logs contain corrected RTT lookup.')
(E/'sync-comparison.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({label:{role:{k:round(v['mean'],4) for k,v in result[label][role].items() if isinstance(v,dict) and 'mean'in v} for role in ['host','client']} for label in ['before','after']},indent=2));print(result['cpu_change_percent'])
