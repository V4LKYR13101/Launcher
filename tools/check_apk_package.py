"""Check the distribution ABI and every native ELF load segment, without extracting libraries."""
import json,struct,sys,zipfile,hashlib
from pathlib import Path
path=Path(sys.argv[1]);problems=[];alignments=set();segments=0
with zipfile.ZipFile(path) as archive:
    libraries=[n for n in archive.namelist() if n.startswith('lib/') and n.endswith('.so')]
    abis=sorted({n.split('/')[1] for n in libraries})
    assert set(abis)=={'arm64-v8a','x86_64'},abis
    for name in libraries:
        data=archive.read(name);assert data[:5]==b'\x7fELF\x02',name
        endian='<' if data[5]==1 else '>';phoff=struct.unpack_from(endian+'Q',data,32)[0];entry_size,count=struct.unpack_from(endian+'HH',data,54)
        for index in range(count):
            kind,flags,offset,address,physical,filesz,memsz,alignment=struct.unpack_from(endian+'IIQQQQQQ',data,phoff+index*entry_size)
            if kind==1:
                segments+=1;alignments.add(alignment)
                if alignment<16384 or offset%16384!=address%16384:problems.append(name)
result={'apk':path.name,'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'abis':abis,'nativeLibraries':len(libraries),'loadSegments':segments,'segmentAlignments':sorted(alignments),'unalignedLibraries':sorted(set(problems))}
assert not problems,result
path.with_suffix('.package-check.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result))
