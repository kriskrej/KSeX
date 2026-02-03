<p align="center">
  <img src="logo.svg" alt="KSeX logo" width="160" />
</p>

# KSeX → Google Sheets (minimal .NET 8)

KSeF to Excel, simple as fuck.

Pobiera metadane faktur z KSeF (sprzedaż i zakupy) dla jednej lub wielu firm i dopisuje nowe wiersze do zakładek w Google Sheets.

## Google Sheets – Service Account
1) Utwórz Service Account i pobierz JSON key.
2) Udostępnij arkusz (Share) na e-mail Service Account.
3) Włącz Sheets API w projekcie Google Cloud.

## Konfiguracja (ENV)
W repo ustaw sekrety (GitHub: Settings → Secrets and variables → Actions):

- GOOGLE_SERVICE_ACCOUNT_JSON_BASE64 (base64 z JSON key)
- LOOKBACK_DAYS (opcjonalnie, domyślnie 7)

Dla każdej firmy (1 i 2):
- COMPANY{n}_NAME
- COMPANY{n}_NIP
- COMPANY{n}_KSEF_TOKEN lub COMPANY{n}_REFRESH_TOKEN
- COMPANY{n}_SPREADSHEET_ID (wymagane)

Zakładki są zawsze tworzone jako `Sprzedaż` (Subject1) oraz `Kupno` (Subject2).

Używany endpoint KSeF jest stały: `https://api.ksef.mf.gov.pl/v2`.
