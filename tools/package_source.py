from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
target = root.parent / 'deliverables/mars-referral-program-portals.zip'
excluded = {'bin', 'obj', '.vs', '.git', 'artifacts', '__pycache__'}
with ZipFile(target, 'w', ZIP_DEFLATED) as z:
    for file in root.rglob('*'):
        relative = file.relative_to(root)
        if file.is_file() and not excluded.intersection(relative.parts):
            z.write(file, Path(root.name) / relative)
with ZipFile(target) as z:
    assert z.testzip() is None
    assert 'mars-referral-program/MarsReferral.sln' in z.namelist()
    assert 'mars-referral-program/src/MarsReferral.Web/wwwroot/guides/mars-referral-guide.pdf' in z.namelist()
    print(f'{target}: {len(z.namelist())} source files, {target.stat().st_size:,} bytes')
