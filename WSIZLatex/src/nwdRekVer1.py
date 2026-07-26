# ************************************************************
# Niezoptymalizowany algorytm Euklidesa - wersja rekurencyjna 
# Dane wejsciowe: a, b
# Dane wyjsciowe: a 
# ************************************************************
def NWD(a,b):
    if a!=b:
        if a>b: return NWD(a-b,b)
        else: return NWD(a,b-a)
    return a