# Kolory ikon umiejętności

Źródłem są zaakceptowane prompty i istniejące ikony z wcześniejszego passu.
Kopie oryginalnych promptów: `IconPaletteFix20261102/convention-sources/`.
Kolor tła pasywki i kolorystyka aktywnej umiejętności są osobnymi regułami.

| Pasywki drzewa | Baza tła | Jaśniejsze płaszczyzny |
| --- | --- | --- |
| Dowódca | granat `#182534` | `#26394c` |
| Awanturnik | bordo `#30151d` | `#511f2c` |
| Skrytobójca | przygaszona śliwka `#281c30` | `#3c2a47` |
| Tancerz Ostrzy | przygaszony teal `#173033` | `#254b50` |
| Strażnik | leśna oliwka `#253023` | `#3c4b34` |
| Czarnoksiężnik | przygaszone indygo `#1b2038` | `#2c3352` |
| Mag Ognia | przygaszony rdzawy brąz `#30211d` | `#493029` |

Zbrojny jest nowym drzewem CP2-06, bez wcześniejszej historycznej palety.
Dla jego pasywek zachowana jest dotychczasowa baza brązowa `#302a20` /
`#4c4030`. Nie stosować jej automatycznie do czterech aktywnych skilli.

Aktywne skille nie dziedziczą automatycznie tła drzewa, które je odblokowuje.
Należy sprawdzić wzorzec tej samej klasy w Skills/VS22:

- Wojownik: aktywne skille zachowują przygaszone bordowe tło, także Bandaż,
  Prawy Sierpowy i Rozkaz Ochrony. Różnią się kolorami efektów na tym tle.
- Zwiadowca: tło zależy od działania — np. Wymiana fioletowoniebieska,
  Zatruty Sztylet oliwkowy, Tonik leśnozielony.
- Mag: Teleportacja korzysta z granatu, cyjanu i fioletu Kuli Energii;
  Słup Ognia z ciemnego borda, pomarańczy i złota Kuli Ognia.
  Ognista Strzała i Strzała Bólu przedstawiają magiczny pocisk ognia,
  bez fizycznego grotu, drzewca i lotek.

CP2-10 zachowuje wspólne bazy oryginalnych promptów Maga oraz surową
referencję stylu B-v2. Kopie źródeł, finalne prompty i pochodzenie generacji:
`Tools/Art/MageT5Icons20261102/`.

Kolory efektów na pierwszym planie i za symbolem:

- fizyczny ruch i kontakt: białosrebrne smugi;
- kontrola/ogłuszenie: przygaszony niebieski;
- przemieszczenie: granat/fiolet i cyjanowe łuki, jak Wymiana;
- trucizna: żółtozielony efekt na ciemnym oliwkowym tle, jak Zatruty Sztylet;
- leczenie/odnowienie: zieleń efektu, jak Bandaż (u Wojownika tło nadal bordowe);
- ochrona: powściągliwe złote światło na ciemnym tle, jak Rozkaz Ochrony;
- magia: niebieskofioletowy; rany/furia: karmazyn.

Materiały pierwszego planu zachowują własne kolory: stal jest srebrna,
skóra brązowa. Nie barwić całego symbolu kolorem drzewa. Tło pozostaje
ciemniejsze od motywu, bez dekoracyjnego powielania promieni i drobnego szumu.

Przy nowej ikonie porównać native 32×32 i powiększenie nearest z co najmniej
jedną istniejącą ikoną właściwego drzewa albo odpowiedniego aktywnego efektu.
Sprawdzenie samej czytelności nie zamyka kontroli zgodności palety.

## CP2-11 — Mag Mrozu

Pasywy: przygaszony niebieski łupek #1c2b33 / #31434f. Lód srebrnobiały i cyjanowy; magiczna ochrona w fiolecie, leczenie zielone, rany czerwone. Aktywne zaklęcia: granatowe tło i cyjanowy lód, konwencja istniejących aktywnych skilli Maga. Każda ikona osobno wygenerowana; pełne płótno nearest32 i roundtrip Aseprite. Prompty i źródła w FrostIcons20261102.
