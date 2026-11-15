"""Preuve de non-référence : python3 Tools/architecture/prove_unreferenced.py <chemin> [<chemin>…] (depuis la racine du projet).
Recherche le GUID de chaque asset/dossier candidat (et de son contenu) dans tous les fichiers de Assets/, ProjectSettings/, Packages/ et Tools/ (texte : forme hexadécimale ; binaires : forme à nibbles inversés) en excluant les candidats eux-mêmes.
Affiche UNREFERENCED si rien ne les référence."""
import os,sys,re,subprocess
root='.'
cands=sys.argv[1:]
def guids_of(p):
    out={}
    paths=[]
    if os.path.isdir(p):
        for d,_,fs in os.walk(p):
            for f in fs:
                if f.endswith('.meta'): paths.append(os.path.join(d,f))
    paths.append(p+'.meta')
    for m in paths:
        if os.path.exists(m):
            g=re.search(r'^guid: ([0-9a-f]{32})',open(m,errors='ignore').read(),re.M)
            if g: out[g.group(1)]=m[:-5]
    return out
allg={}
for c in cands:
    for g,p in guids_of(c).items(): allg[g]=(c,p)
print(len(allg),'guids')
# gather scan files: everything under Assets, ProjectSettings, Packages, Tools, Docs excluding candidates themselves
excl=tuple(os.path.normpath(c) for c in cands)
hits={g:[] for g in allg}
def rev(g):  # nibble-reversed binary form
    return bytes(int(g[i+1]+g[i],16) for i in range(0,32,2))
revmap={rev(g):g for g in allg}
for base in ['Assets','ProjectSettings','Packages','Tools']:
    for d,_,fs in os.walk(base):
        for f in fs:
            p=os.path.join(d,f)
            n=os.path.normpath(p)
            if any(n==e or n.startswith(e+os.sep) or n==e+'.meta' for e in excl): continue
            if f.endswith('.meta'): continue
            try:
                sz=os.path.getsize(p)
                if sz>400_000_000: continue
                b=open(p,'rb').read()
            except Exception: continue
            for g in allg:
                if g.encode() in b: hits[g].append(p)
            for r,g in revmap.items():
                if r in b: hits[g].append(p+' [binary]')
bad={g:h for g,h in hits.items() if h}
for g,h in bad.items(): print('REFERENCED',allg[g][1],'<-',h[:3])
print('UNREFERENCED' if not bad else 'HAS_REFS', len(allg)-len(bad),'/',len(allg))
