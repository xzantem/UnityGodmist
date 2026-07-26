# ************************************************************
# Zoptymalizowany algorytm Euklidesa - wersja rekurencyjna 
# Dane wejsciowe: a, b
# Dane wyjsciowe: a 
# ************************************************************
def nwdrek(a,b):
    if b!=0:
        return nwdrek(b, a % b)
    return a