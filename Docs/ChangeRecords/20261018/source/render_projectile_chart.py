from pathlib import Path
import csv,subprocess
from reportlab.graphics.shapes import Drawing,String,Line
from reportlab.graphics.charts.lineplots import LinePlot
from reportlab.graphics import renderPDF
from reportlab.lib.colors import HexColor
D=Path(__file__).resolve().parent.parent
plot=LinePlot();plot.x=55;plot.y=65;plot.width=575;plot.height=195
series=[]
for stage in ['before','after']:
 rows=list(csv.DictReader((D/f'evidence/projectile-display-{stage}.csv').open(encoding='utf-8')))
 series.append([(float(r['time']),float(r['position_x'])) for r in rows if r['kind']=='9' and .05<=float(r['time'])<=.36])
plot.data=series;plot.xValueAxis.valueMin=.05;plot.xValueAxis.valueMax=.36;plot.xValueAxis.valueStep=.05
plot.yValueAxis.valueMin=0;plot.yValueAxis.valueMax=18;plot.yValueAxis.valueStep=3
plot.xValueAxis.labels.fontSize=9;plot.yValueAxis.labels.fontSize=9
colors=[HexColor('#bb452b'),HexColor('#087e8b')]
for i,color in enumerate(colors):plot.lines[i].strokeColor=color;plot.lines[i].strokeWidth=1.7
draw=Drawing(660,320);draw.add(plot)
draw.add(String(330,302,'Measured Unity Play display with 30 Hz scripted snapshots',textAnchor='middle',fontName='Helvetica-Bold',fontSize=12))
draw.add(String(330,12,'Time since simulated release (s)',textAnchor='middle',fontSize=10))
draw.add(String(55,275,'Displayed travel distance (m)',fontSize=10))
for x,label,color in [(75,'Before: snapshot position only',colors[0]),(340,'After: velocity each frame',colors[1])]:
 draw.add(Line(x,32,x+22,32,strokeColor=color,strokeWidth=2));draw.add(String(x+29,29,label,fontSize=9))
pdf=D/'evidence/projectile-motion-plot.pdf';renderPDF.drawToFile(draw,str(pdf))
subprocess.run([r'C:/Users/xlfhf/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/poppler/Library/bin/pdftoppm.exe','-scale-to','1800','-singlefile','-png',str(pdf),str(D/'images/20261018_projectile_motion_comparison')],check=True)
