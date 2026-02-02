# KSeF → Google Sheets (minimal .NET 8)

Pobiera metadane faktur z KSeF (sprzedaż i zakupy) dla 2 firm i dopisuje nowe wiersze do 4 zakładek w Google Sheets.

## Google Sheets – Service Account
1) Utwórz Service Account i pobierz JSON key.
2) Udostępnij arkusz (Share) na e-mail Service Account.
3) Włącz Sheets API w projekcie Google Cloud.

## Konfiguracja (ENV)
W repo ustaw sekrety (GitHub: Settings → Secrets and variables → Actions):

- SPREADSHEET_ID
- GOOGLE_SERVICE_ACCOUNT_JSON_BASE64 (base64 z JSON key)
- KSEF_BASE_URL (opcjonalnie, domyślnie https://api.ksef.mf.gov.pl/v2)
- LOOKBACK_DAYS (opcjonalnie, domyślnie 7)

Dla każdej firmy (1 i 2):
- COMPANY{n}_NAME
- COMPANY{n}_NIP
- COMPANY{n}_KSEF_TOKEN lub COMPANY{n}_REFRESH_TOKEN
- COMPANY{n}_SALES_TAB / COMPANY{n}_PURCHASES_TAB (opcjonalnie)
- COMPANY{n}_SUBJECT_TYPE (opcjonalnie, domyślnie Subject1)

