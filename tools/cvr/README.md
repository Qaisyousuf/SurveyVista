# CVR-udtræk – branchekode 325000

Henter alle **aktive** danske virksomheder i branchekode `325000`
("Fremstilling af medicinske og dentale instrumenter samt udstyr") fra
Erhvervsstyrelsens CVR-distributions-API og gemmer dem som CSV.

## Login

`CVR_BRUGER` og `CVR_KODE` er **obligatoriske**. Der er bevidst ingen indbyggede
standardværdier – credentials hører ikke hjemme i versionsstyret kode. Mangler
en af dem, stopper scriptet med det samme og fortæller hvilken.

```bash
export CVR_BRUGER=DIT_BRUGERNAVN
export CVR_KODE=DIN_ADGANGSKODE
```

Undgå at skrive `export CVR_KODE=...` direkte i terminalen, hvis din shell
gemmer historik – læg det i stedet i en fil uden for repoet (f.eks. `~/.cvr.env`
med `chmod 600`) og kør `source ~/.cvr.env`.

## Kørsel

```bash
pip install requests
cd tools/cvr

python3 hent_branche_325000.py             # hent alt -> cvr_branche_325000.csv + opsummering
python3 hent_branche_325000.py --probe     # dump ét råt hit til raa_hit.json og vis feltstier
python3 hent_branche_325000.py --selvtest  # test parsing/filter/CSV uden netværk (kræver ikke login)
```

### Flag

| Flag | Betydning |
| --- | --- |
| `--probe` | Henter ét hit, gemmer råt JSON og printer nøglerne i `Vrvirksomhed` / `virksomhedMetadata`. Brug denne hvis feltstierne har ændret sig. |
| `--selvtest` | Kører udtræk, ophørs-filter, CSV-skrivning og opsummering på syntetiske data. Intet netværk. |
| `--inkluder-bibrancher` | Medtag også virksomheder hvor 325000 er bibranche 1–3 (standard: kun hovedbranche). |
| `--alle-statusser` | Frasortér **ikke** ophørte virksomheder. |
| `-o FIL` | Andet CSV-filnavn. |

## Output

CSV skrives med `utf-8-sig` (BOM) og semikolon som separator, så Excel på dansk
Windows åbner den direkte med korrekte æ/ø/å. Kolonner:

`cvr_nummer; virksomhedsnavn; adresse; postnummer; by; virksomhedsform;
ansatte_interval; email; telefon; stiftelsesdato; status`

Til sidst printes en opsummering: antal i alt, antal frasorteret, antal med
`dental`/`tandtekni` i navnet, antal med `bandag`/`ortop` i navnet, og
fordelingen på postnummer-regioner.

## Feltstier i svaret

Data ligger under `_source.Vrvirksomhed`. De centrale stier:

| Felt | Sti |
| --- | --- |
| CVR-nummer | `cvrNummer` |
| Navn | `virksomhedMetadata.nyesteNavn.navn` |
| Adresse | `virksomhedMetadata.nyesteBeliggenhedsadresse` (samles af `vejnavn`, `husnummerFra`, `bogstavFra`, `etage`, `sidedoer`) |
| Postnr./by | `…nyesteBeliggenhedsadresse.postnummer` / `.postdistrikt` |
| Virksomhedsform | `virksomhedMetadata.nyesteVirksomhedsform.langBeskrivelse` |
| Ansatte | `virksomhedMetadata.nyesteErstMaanedsbeskaeftigelse.intervalKodeAntalAnsatte` (falder tilbage til kvartal/år) |
| E-mail | `elektroniskPost[].kontaktoplysning` (aktuel periode, `hemmelig` springes over) |
| Telefon | `telefonNummer[].kontaktoplysning` (samme regel) |
| Stiftelsesdato | `virksomhedMetadata.stiftelsesDato` |
| Status | `virksomhedMetadata.sammensatStatus` |

Alle opslag går gennem hjælperen `dyk()` med fallback-stier, så et enkelt
omdøbt felt ikke vælter udtrækket. Afviger strukturen alligevel, så kør
`--probe` og ret stierne øverst i de relevante funktioner.

## Sådan afgøres "aktiv"

1. `livsforloeb` skal have mindst én periode uden `gyldigTil` (åbent livsforløb).
2. `sammensatStatus` må ikke være en ophørs-status (`OPHØRT`, `OPLØST`,
   `TVANGSOPLØST`, `SLETTET`, `OPLØST EFTER KONKURS`, …).

Virksomheder under konkurs/likvidation er stadig registrerede og tælles med;
brug CSV-kolonnen `status` hvis de skal sorteres fra bagefter.

## Sideinddeling

Udtrækket bruger `search_after` sorteret på `cvrNummer` i sider à 1000, så
Elasticsearch' loft på 10.000 rækker for `from`/`size` ikke rammes. Kald mod
API'et prøves igen med eksponentiel backoff (2s, 4s, 8s, 16s) ved netværksfejl
og 5xx; 401/403 fejler med det samme.

## Netværkskrav

Scriptet kalder `http://distribution.virk.dk`. Kører du det i et miljø med
egress-allowlist (fx Claude Code på nettet), skal `distribution.virk.dk` være
tilladt, ellers svarer proxyen 403 "Host not in allowlist".
