# Godmist — testing rules

## Owner

Główny wątek sam odpowiada za automatyczną weryfikację i naprawę failure.

## Kolejność

Dobierz najmniejszy wiarygodny zestaw:
1. final diff/code inspection;
2. compile/build;
3. focused unit/EditMode tests;
4. focused PlayMode tests tylko jeśli mają sens;
5. broader regression tylko przy realnym cross-system/save/migration risk.

Nie uruchamiaj pełnego suite wielokrotnie bez konkretnego powodu.

## Failure

`failure → konkretny root cause → minimalna poprawka → targeted retest`

Nie zmieniaj testu tylko po to, żeby był zielony.

Jeśli failure ujawnia brakującą decyzję gameplay/UX/product, zatrzymaj się po decyzję.

## Evidence

Rozróżniaj:
- `PASS` — faktycznie uruchomione i zakończone sukcesem;
- `FAIL` — faktycznie uruchomione i zakończone błędem;
- `NOT RUN / UNVERIFIED` — niewykonane lub niemożliwe do sprawdzenia.

Nie przedstawiaj planowanego/rozpoczętego testu jako wykonanego.

## Smoke test

Manualny Unity smoke test wykonuje użytkownik po `READY_FOR_SMOKE`.

Jeżeli potrzebny jest Editor setup, kroki i oczekiwany rezultat znajdują się
w `UNITY_HANDOFF.md`.

Commit/push dopiero po `git`.
