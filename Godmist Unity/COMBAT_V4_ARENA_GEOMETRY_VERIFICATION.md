# Combat v4 — granice i geometria areny

Status: DONE — smoke zaakceptowany, commit/push zatwierdzone.

- PASS: 56/56 EditMode; TEMP/Godmist_CombatV4_Verification/arena-final.xml.
- PASS: kompilacja aktualnego kodu w izolowanym projekcie Unity 6000.4.0f1.
- Zakres: 29 seamless, 4 LOS, 6 grid factory, 3 dungeon integration, 14 AI.
- Nowe przypadki: O-room z przestrzenią poza areną, ściana całkowicie poza footprintem, ściana graniczna w kontekście, wyjście bez drzwi, zajęte wyjście, przeszkoda blokująca ruch niezależnie od LOS, zmiana stanu drzwi poza areną podczas tej samej sesji.
- PASS: final self-review oraz git diff --check.
- PASS: manualny smoke geometrii zaakceptowany przez użytkownika.
- Brak zmian serialized state oraz save schema.
