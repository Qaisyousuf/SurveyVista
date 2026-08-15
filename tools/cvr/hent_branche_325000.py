#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Henter alle aktive danske virksomheder i branchekode 325000
("Fremstilling af medicinske og dentale instrumenter samt udstyr")
fra Erhvervsstyrelsens CVR-distributionsAPI (Elasticsearch).

Brug:
    python3 hent_branche_325000.py                  # hent alt -> CSV + opsummering
    python3 hent_branche_325000.py --probe          # dump ét råt hit, så feltstier kan verificeres
    python3 hent_branche_325000.py --selvtest       # kør parsing/filter/CSV-logik på syntetiske data
    python3 hent_branche_325000.py --inkluder-bibrancher
    python3 hent_branche_325000.py --alle-statusser # frasortér IKKE ophørte

Login SKAL sættes i miljøvariablerne CVR_BRUGER og CVR_KODE:

    export CVR_BRUGER=DIT_BRUGERNAVN
    export CVR_KODE=DIN_ADGANGSKODE

Der er med vilje ingen indbyggede standardværdier – credentials hører ikke
hjemme i versionsstyret kode. (--selvtest kører uden login.)
"""

import argparse
import csv
import json
import os
import sys
import time
from collections import Counter

import requests
from requests.auth import HTTPBasicAuth

URL = "http://distribution.virk.dk/cvr-permanent/virksomhed/_search"
BRANCHEKODE = "325000"
SIDESTOERRELSE = 1000
UDFIL = "cvr_branche_325000.csv"

HOVEDBRANCHE_FELTER = [
    "Vrvirksomhed.virksomhedMetadata.nyesteHovedbranche.branchekode",
]
BIBRANCHE_FELTER = [
    "Vrvirksomhed.virksomhedMetadata.nyesteBibranche1.branchekode",
    "Vrvirksomhed.virksomhedMetadata.nyesteBibranche2.branchekode",
    "Vrvirksomhed.virksomhedMetadata.nyesteBibranche3.branchekode",
]

# Statusser der betyder at virksomheden er ophørt/udgået.
OPHOERT_STATUS = {
    "OPHØRT", "OPLØST", "SLETTET", "UDGÅET", "TVANGSOPLØST",
    "OPLØST EFTER KONKURS", "OPLØST EFTER ERKLÆRING", "OPLØST EFTER FUSION",
    "OPLØST EFTER SPALTNING", "OPLØST EFTER GRÆNSEOVERSKRIDENDE FUSION",
    "OPLØST EFTER GRÆNSEOVERSKRIDENDE SPALTNING", "OPLØST EFTER FRIVILLIG LIKVIDATION",
}

CSV_KOLONNER = [
    "cvr_nummer", "virksomhedsnavn", "adresse", "postnummer", "by",
    "virksomhedsform", "ansatte_interval", "email", "telefon",
    "stiftelsesdato", "status",
]


# --------------------------------------------------------------------------
# Hjælpefunktioner til at grave i det (dybt indlejrede) CVR-svar
# --------------------------------------------------------------------------

def dyk(obj, *stier, standard=None):
    """Returnér første ikke-tomme værdi blandt flere punktseparerede stier."""
    for sti in stier:
        node = obj
        for noegle in sti.split("."):
            if isinstance(node, dict):
                node = node.get(noegle)
            elif isinstance(node, list) and node:
                node = node[0].get(noegle) if isinstance(node[0], dict) else None
            else:
                node = None
                break
        if node not in (None, "", [], {}):
            return node
    return standard


def nyeste_gyldige(poster):
    """
    Vælg den aktuelle post i en CVR-liste af {..., periode:{gyldigFra, gyldigTil}}.
    Foretræk posten uden slutdato (stadig gyldig), ellers den med seneste gyldigFra.
    """
    if not isinstance(poster, list) or not poster:
        return None
    aabne = [p for p in poster
             if isinstance(p, dict) and (p.get("periode") or {}).get("gyldigTil") in (None, "")]
    kandidater = aabne or [p for p in poster if isinstance(p, dict)]
    if not kandidater:
        return None
    return max(kandidater, key=lambda p: ((p.get("periode") or {}).get("gyldigFra") or ""))


def kontakt(virk, felt):
    """Træk aktuel e-mail/telefon ud; spring hemmeligt markerede oplysninger over."""
    poster = virk.get(felt)
    if not isinstance(poster, list):
        return ""
    synlige = [p for p in poster if isinstance(p, dict) and not p.get("hemmelig")]
    valgt = nyeste_gyldige(synlige)
    if not valgt:
        return ""
    return (valgt.get("kontaktoplysning") or "").strip()


def byg_adresse(adr):
    """Sæt vejnavn, husnummer, bogstav, etage og dør sammen til én adresselinje."""
    if not isinstance(adr, dict):
        return ""
    if adr.get("fritekst"):
        return str(adr["fritekst"]).strip()

    husnr = adr.get("husnummerFra")
    dele = [adr.get("vejnavn")]
    if husnr not in (None, ""):
        nr = str(husnr)
        if adr.get("bogstavFra"):
            nr += str(adr["bogstavFra"])
        if adr.get("husnummerTil") not in (None, "") and adr.get("husnummerTil") != husnr:
            til = str(adr["husnummerTil"])
            if adr.get("bogstavTil"):
                til += str(adr["bogstavTil"])
            nr += "-" + til
        dele.append(nr)

    linje = " ".join(str(d) for d in dele if d not in (None, "")).strip()
    if adr.get("etage"):
        linje += f", {adr['etage']}."
    if adr.get("sidedoer"):
        linje += f" {adr['sidedoer']}"
    if adr.get("conavn"):
        linje = f"c/o {adr['conavn']}, {linje}" if linje else f"c/o {adr['conavn']}"
    return linje.strip(" ,")


def formatér_ansatte(beskaeftigelse):
    """
    Lav CVR'ens intervalkode om til noget læsbart.
    'ANTAL_2_4' -> '2-4', 'ANTAL_1_1' -> '1', 'ANTAL_1000_' -> '1000+'.
    """
    if not isinstance(beskaeftigelse, dict):
        return ""
    kode = beskaeftigelse.get("intervalKodeAntalAnsatte")
    if kode:
        stykker = str(kode).replace("ANTAL_", "").split("_")
        if len(stykker) == 2:
            lav, hoej = stykker
            if not hoej:
                return f"{lav}+"
            return lav if lav == hoej else f"{lav}-{hoej}"
        return str(kode)
    antal = beskaeftigelse.get("antalAnsatte")
    return str(antal) if antal not in (None, "") else ""


def hent_beskaeftigelse(meta):
    """Nyeste beskæftigelse: måned før kvartal før år."""
    for felt in ("nyesteErstMaanedsbeskaeftigelse", "nyesteMaanedsbeskaeftigelse",
                 "nyesteKvartalsbeskaeftigelse", "nyesteAarsbeskaeftigelse"):
        vaerdi = meta.get(felt)
        if isinstance(vaerdi, dict) and vaerdi:
            tekst = formatér_ansatte(vaerdi)
            if tekst:
                return tekst
    return ""


def er_aktiv(virk, meta):
    """
    Aktiv = livsforløbet er stadig åbent OG statussen er ikke en ophørs-status.
    livsforloeb er den mest pålidelige kilde; sammensatStatus fanger resten.
    """
    livsforloeb = virk.get("livsforloeb")
    if isinstance(livsforloeb, list) and livsforloeb:
        aaben = any((p.get("periode") or {}).get("gyldigTil") in (None, "")
                    for p in livsforloeb if isinstance(p, dict))
        if not aaben:
            return False

    status = (hent_status(virk, meta) or "").strip().upper()
    if status in OPHOERT_STATUS:
        return False
    return True


def hent_status(virk, meta):
    status = meta.get("sammensatStatus")
    if status:
        return str(status)
    nyeste = nyeste_gyldige(virk.get("virksomhedsstatus"))
    if nyeste and nyeste.get("status"):
        return str(nyeste["status"])
    livsforloeb = virk.get("livsforloeb")
    if isinstance(livsforloeb, list) and livsforloeb:
        aaben = any((p.get("periode") or {}).get("gyldigTil") in (None, "")
                    for p in livsforloeb if isinstance(p, dict))
        return "AKTIV" if aaben else "OPHØRT"
    return ""


def udtræk(hit):
    """Lav ét Elasticsearch-hit om til én CSV-række."""
    kilde = hit.get("_source", hit)
    virk = kilde.get("Vrvirksomhed", kilde)
    meta = virk.get("virksomhedMetadata") or {}

    adr = (meta.get("nyesteBeliggenhedsadresse")
           or meta.get("nyestePostadresse")
           or {})

    return {
        "cvr_nummer": str(virk.get("cvrNummer") or ""),
        "virksomhedsnavn": (dyk(meta, "nyesteNavn.navn", standard="")
                            or dyk(virk, "navne.navn", standard="") or ""),
        "adresse": byg_adresse(adr),
        "postnummer": str(adr.get("postnummer") or ""),
        "by": str(adr.get("postdistrikt") or ""),
        "virksomhedsform": (dyk(meta, "nyesteVirksomhedsform.langBeskrivelse",
                                "nyesteVirksomhedsform.kortBeskrivelse", standard="") or ""),
        "ansatte_interval": hent_beskaeftigelse(meta),
        "email": kontakt(virk, "elektroniskPost"),
        "telefon": kontakt(virk, "telefonNummer"),
        "stiftelsesdato": str(meta.get("stiftelsesDato") or ""),
        "status": hent_status(virk, meta),
    }, virk, meta


# --------------------------------------------------------------------------
# API-kald
# --------------------------------------------------------------------------

def hent_login():
    """
    Læs CVR-login fra miljøet. Begge variabler er obligatoriske – der er
    bevidst ingen fallback, så en adgangskode ikke kan ende i git.
    """
    bruger = (os.environ.get("CVR_BRUGER") or "").strip()
    kode = os.environ.get("CVR_KODE") or ""
    mangler = [navn for navn, vaerdi in (("CVR_BRUGER", bruger), ("CVR_KODE", kode))
               if not vaerdi]
    if mangler:
        raise SystemExit(
            "FEJL: manglende miljøvariabler: " + ", ".join(mangler) + "\n\n"
            "Sæt CVR-login før kørsel, f.eks.:\n"
            "    export CVR_BRUGER=DIT_BRUGERNAVN\n"
            "    export CVR_KODE=DIN_ADGANGSKODE\n\n"
            "Kør 'python3 hent_branche_325000.py --selvtest' for at teste uden login."
        )
    return HTTPBasicAuth(bruger, kode)


def søgning(inkluder_bibrancher=False):
    felter = list(HOVEDBRANCHE_FELTER)
    if inkluder_bibrancher:
        felter += BIBRANCHE_FELTER
    return {
        "bool": {
            "should": [{"term": {f: BRANCHEKODE}} for f in felter],
            "minimum_should_match": 1,
        }
    }


def kald(krop, forsøg=4):
    """POST mod CVR-API'et med backoff på netværks-/5xx-fejl."""
    auth = hent_login()
    ventetid = 2
    for n in range(forsøg):
        try:
            svar = requests.post(
                URL,
                auth=auth,
                json=krop,
                headers={"Content-Type": "application/json"},
                timeout=120,
            )
            if svar.status_code == 200:
                return svar.json()
            if svar.status_code in (401, 403):
                raise SystemExit(
                    f"FEJL {svar.status_code}: afvist af CVR-API'et. "
                    f"Tjek værdierne i CVR_BRUGER / CVR_KODE.\n{svar.text[:500]}"
                )
            if svar.status_code < 500:
                raise SystemExit(f"FEJL {svar.status_code}: {svar.text[:800]}")
            print(f"  HTTP {svar.status_code} – prøver igen om {ventetid}s", file=sys.stderr)
        except requests.RequestException as fejl:
            if n == forsøg - 1:
                raise
            print(f"  Netværksfejl ({type(fejl).__name__}) – prøver igen om {ventetid}s",
                  file=sys.stderr)
        time.sleep(ventetid)
        ventetid *= 2
    raise SystemExit("Gav op efter gentagne fejl mod CVR-API'et.")


def probe(inkluder_bibrancher=False):
    """Hent ét råt hit, gem det, og vis hvilke feltstier der faktisk findes."""
    data = kald({"size": 1, "query": søgning(inkluder_bibrancher)})
    hits = data.get("hits", {}).get("hits", [])
    print("Total ifølge API:", json.dumps(data.get("hits", {}).get("total")))
    if not hits:
        print("Ingen hits – tjek feltsti/branchekode.")
        return
    with open("raa_hit.json", "w", encoding="utf-8") as f:
        json.dump(hits[0], f, indent=2, ensure_ascii=False)
    print("Råt hit gemt i raa_hit.json")

    kilde = hits[0].get("_source", {})
    virk = kilde.get("Vrvirksomhed", kilde)
    meta = virk.get("virksomhedMetadata") or {}
    print("\nNøgler i Vrvirksomhed:", sorted(virk.keys()))
    print("\nNøgler i virksomhedMetadata:", sorted(meta.keys()))
    række, _, _ = udtræk(hits[0])
    print("\nUdtrukket række:")
    for k, v in række.items():
        print(f"  {k:18} = {v!r}")


def hent_alle(inkluder_bibrancher=False, alle_statusser=False):
    """
    Sideinddeling med search_after (sorteret på _doc-stabilt cvrNummer),
    så vi ikke rammer Elasticsearch' 10.000-loft på from/size.
    """
    rækker, frasorteret, set_cvr = [], 0, set()
    efter = None
    side = 0

    while True:
        krop = {
            "size": SIDESTOERRELSE,
            "query": søgning(inkluder_bibrancher),
            "sort": [{"Vrvirksomhed.cvrNummer": "asc"}],
        }
        if efter is not None:
            krop["search_after"] = efter

        data = kald(krop)
        hits = data.get("hits", {}).get("hits", [])
        if not hits:
            break

        side += 1
        for hit in hits:
            række, virk, meta = udtræk(hit)
            if not række["cvr_nummer"] or række["cvr_nummer"] in set_cvr:
                continue
            set_cvr.add(række["cvr_nummer"])
            if not alle_statusser and not er_aktiv(virk, meta):
                frasorteret += 1
                continue
            rækker.append(række)

        efter = hits[-1].get("sort")
        print(f"  side {side}: {len(hits)} hits – {len(rækker)} aktive indtil nu",
              file=sys.stderr)
        if len(hits) < SIDESTOERRELSE or not efter:
            break

    return rækker, frasorteret


# --------------------------------------------------------------------------
# Opsummering
# --------------------------------------------------------------------------

REGIONER = [
    (1000, 1499, "1000-1499 København K"),
    (1500, 1799, "1500-1799 København V"),
    (1800, 1999, "1800-1999 Frederiksberg C"),
    (2000, 2999, "2000-2999 København omegn"),
    (3000, 3699, "3000-3699 Nordsjælland"),
    (3700, 3799, "3700-3799 Bornholm"),
    (3900, 3999, "3900-3999 Grønland"),
    (4000, 4999, "4000-4999 Sjælland og øerne"),
    (5000, 5999, "5000-5999 Fyn"),
    (6000, 6999, "6000-6999 Syd- og Sønderjylland"),
    (7000, 7999, "7000-7999 Midt- og Vestjylland"),
    (8000, 8999, "8000-8999 Østjylland"),
    (9000, 9999, "9000-9999 Nordjylland"),
]


def region(postnummer):
    try:
        nr = int(str(postnummer).strip())
    except (TypeError, ValueError):
        return "Ukendt/udland"
    for lav, hoej, navn in REGIONER:
        if lav <= nr <= hoej:
            return navn
    return "Ukendt/udland"


def tæl_navne(rækker, nøgleord):
    return sum(1 for r in rækker
               if any(o in (r["virksomhedsnavn"] or "").lower() for o in nøgleord))


def opsummer(rækker, frasorteret, udfil):
    i_alt = len(rækker)
    dental = tæl_navne(rækker, ("dental", "tandtekni"))
    bandage = tæl_navne(rækker, ("bandag", "ortop"))

    print()
    print("=" * 60)
    print(f"OPSUMMERING – branchekode {BRANCHEKODE}")
    print("=" * 60)
    print(f"Aktive virksomheder i alt      : {i_alt}")
    print(f"Frasorteret (ophørte m.v.)     : {frasorteret}")
    print(f"Navn med 'dental'/'tandtekni'  : {dental}"
          + (f"  ({dental / i_alt * 100:.1f} %)" if i_alt else ""))
    print(f"Navn med 'bandag'/'ortop'      : {bandage}"
          + (f"  ({bandage / i_alt * 100:.1f} %)" if i_alt else ""))

    print("\nFordeling på postnummer-regioner:")
    tælling = Counter(region(r["postnummer"]) for r in rækker)
    raekkefoelge = [n for _, _, n in REGIONER] + ["Ukendt/udland"]
    for navn in raekkefoelge:
        antal = tælling.get(navn, 0)
        if not antal:
            continue
        andel = antal / i_alt * 100 if i_alt else 0
        bjælke = "#" * int(round(andel / 2))
        print(f"  {navn:32} {antal:5}  {andel:5.1f} %  {bjælke}")

    med_email = sum(1 for r in rækker if r["email"])
    med_tlf = sum(1 for r in rækker if r["telefon"])
    print(f"\nMed e-mail: {med_email}   Med telefon: {med_tlf}")
    print(f"\nGemt i: {udfil}")


def skriv_csv(rækker, udfil):
    with open(udfil, "w", encoding="utf-8-sig", newline="") as f:
        skriver = csv.DictWriter(f, fieldnames=CSV_KOLONNER, delimiter=";")
        skriver.writeheader()
        for r in sorted(rækker, key=lambda x: (x["postnummer"], x["virksomhedsnavn"])):
            skriver.writerow(r)


# --------------------------------------------------------------------------
# Selvtest – kører parsing/filter/CSV/opsummering uden netværk
# --------------------------------------------------------------------------

def selvtest():
    prøver = [
        {"_source": {"Vrvirksomhed": {
            "cvrNummer": 12345678,
            "livsforloeb": [{"periode": {"gyldigFra": "2001-01-01", "gyldigTil": None}}],
            "elektroniskPost": [
                {"kontaktoplysning": "gammel@x.dk", "hemmelig": False,
                 "periode": {"gyldigFra": "2001-01-01", "gyldigTil": "2010-01-01"}},
                {"kontaktoplysning": "info@dentallab.dk", "hemmelig": False,
                 "periode": {"gyldigFra": "2010-01-02", "gyldigTil": None}}],
            "telefonNummer": [{"kontaktoplysning": "12345678", "hemmelig": False,
                               "periode": {"gyldigFra": "2001-01-01", "gyldigTil": None}}],
            "virksomhedMetadata": {
                "nyesteNavn": {"navn": "Dental Teknik ApS"},
                "sammensatStatus": "NORMAL",
                "stiftelsesDato": "2001-01-01",
                "nyesteVirksomhedsform": {"langBeskrivelse": "Anpartsselskab"},
                "nyesteErstMaanedsbeskaeftigelse": {"intervalKodeAntalAnsatte": "ANTAL_2_4"},
                "nyesteBeliggenhedsadresse": {
                    "vejnavn": "Hovedgaden", "husnummerFra": 12, "bogstavFra": "A",
                    "etage": "2", "sidedoer": "tv", "postnummer": 8000,
                    "postdistrikt": "Aarhus C"}}}}},
        # Ophørt – skal frasorteres (lukket livsforløb)
        {"_source": {"Vrvirksomhed": {
            "cvrNummer": 22222222,
            "livsforloeb": [{"periode": {"gyldigFra": "1990-01-01", "gyldigTil": "2015-06-01"}}],
            "virksomhedMetadata": {
                "nyesteNavn": {"navn": "Ortopædi Nord A/S"},
                "sammensatStatus": "OPHØRT", "stiftelsesDato": "1990-01-01",
                "nyesteBeliggenhedsadresse": {"postnummer": 9000, "postdistrikt": "Aalborg"}}}}},
        # Aktiv, hemmelig e-mail, årsbeskæftigelse, åbent interval
        {"_source": {"Vrvirksomhed": {
            "cvrNummer": 33333333,
            "livsforloeb": [{"periode": {"gyldigFra": "1980-01-01", "gyldigTil": None}}],
            "elektroniskPost": [{"kontaktoplysning": "skjult@x.dk", "hemmelig": True,
                                 "periode": {"gyldigFra": "1980-01-01", "gyldigTil": None}}],
            "virksomhedMetadata": {
                "nyesteNavn": {"navn": "Bandagist Centret I/S"},
                "sammensatStatus": "NORMAL", "stiftelsesDato": "1980-01-01",
                "nyesteVirksomhedsform": {"langBeskrivelse": "Interessentskab"},
                "nyesteAarsbeskaeftigelse": {"intervalKodeAntalAnsatte": "ANTAL_1000_"},
                "nyesteBeliggenhedsadresse": {
                    "vejnavn": "Æblevej", "husnummerFra": 3, "postnummer": 1050,
                    "postdistrikt": "København K"}}}}},
        # Aktiv, men status TVANGSOPLØST -> frasorteres selvom livsforløb er åbent
        {"_source": {"Vrvirksomhed": {
            "cvrNummer": 44444444,
            "livsforloeb": [{"periode": {"gyldigFra": "2005-01-01", "gyldigTil": None}}],
            "virksomhedMetadata": {
                "nyesteNavn": {"navn": "Tandteknik Syd ApS"},
                "sammensatStatus": "TVANGSOPLØST", "stiftelsesDato": "2005-01-01",
                "nyesteBeliggenhedsadresse": {"postnummer": 6000, "postdistrikt": "Kolding"}}}}},
    ]

    rækker, frasorteret = [], 0
    for hit in prøver:
        række, virk, meta = udtræk(hit)
        if not er_aktiv(virk, meta):
            frasorteret += 1
            continue
        rækker.append(række)

    fejl = []

    def tjek(betingelse, besked):
        if not betingelse:
            fejl.append(besked)

    tjek(len(rækker) == 2, f"forventede 2 aktive, fik {len(rækker)}")
    tjek(frasorteret == 2, f"forventede 2 frasorterede, fik {frasorteret}")

    a = next(r for r in rækker if r["cvr_nummer"] == "12345678")
    tjek(a["adresse"] == "Hovedgaden 12A, 2. tv", f"adresse: {a['adresse']!r}")
    tjek(a["email"] == "info@dentallab.dk", f"email: {a['email']!r}")
    tjek(a["telefon"] == "12345678", f"telefon: {a['telefon']!r}")
    tjek(a["ansatte_interval"] == "2-4", f"ansatte: {a['ansatte_interval']!r}")
    tjek(a["virksomhedsform"] == "Anpartsselskab", f"form: {a['virksomhedsform']!r}")
    tjek(a["by"] == "Aarhus C", f"by: {a['by']!r}")

    b = next(r for r in rækker if r["cvr_nummer"] == "33333333")
    tjek(b["email"] == "", "hemmelig e-mail burde være udeladt")
    tjek(b["ansatte_interval"] == "1000+", f"åbent interval: {b['ansatte_interval']!r}")
    tjek(b["adresse"] == "Æblevej 3", f"adresse: {b['adresse']!r}")

    tjek(tæl_navne(rækker, ("dental", "tandtekni")) == 1, "dental-tælling")
    tjek(tæl_navne(rækker, ("bandag", "ortop")) == 1, "bandage-tælling")
    tjek(region(8000) == "8000-8999 Østjylland", "region 8000")
    tjek(region(1050) == "1000-1499 København K", "region 1050")
    tjek(region("") == "Ukendt/udland", "tom region")

    udfil = "selvtest_" + UDFIL
    skriv_csv(rækker, udfil)
    with open(udfil, "rb") as f:
        raa = f.read()
    tjek(raa.startswith(b"\xef\xbb\xbf"), "CSV mangler UTF-8 BOM")
    tjek("Æblevej".encode("utf-8") in raa, "æøå ikke korrekt kodet")
    with open(udfil, encoding="utf-8-sig") as f:
        genlæst = list(csv.DictReader(f, delimiter=";"))
    tjek(len(genlæst) == 2, f"CSV genlæst: {len(genlæst)} rækker")
    tjek(list(genlæst[0].keys()) == CSV_KOLONNER, "CSV-kolonner afviger")

    if fejl:
        print("SELVTEST FEJLEDE:")
        for f_ in fejl:
            print("  -", f_)
        return 1

    print("Selvtest OK – parsing, ophørs-filter, CSV (utf-8-sig) og opsummering virker.")
    opsummer(rækker, frasorteret, udfil)
    os.remove(udfil)
    return 0


def main():
    p = argparse.ArgumentParser(description="Hent aktive virksomheder i branchekode 325000 fra CVR.")
    p.add_argument("--probe", action="store_true", help="dump ét råt hit og vis feltstier")
    p.add_argument("--selvtest", action="store_true", help="kør logiktest uden netværk")
    p.add_argument("--inkluder-bibrancher", action="store_true",
                   help="medtag virksomheder hvor 325000 er bibranche")
    p.add_argument("--alle-statusser", action="store_true", help="frasortér IKKE ophørte")
    p.add_argument("-o", "--ud", default=UDFIL, help=f"CSV-filnavn (standard: {UDFIL})")
    a = p.parse_args()

    if a.selvtest:
        return selvtest()

    hent_login()  # fejl tidligt og tydeligt, før vi går i gang

    if a.probe:
        probe(a.inkluder_bibrancher)
        return 0

    print(f"Henter branchekode {BRANCHEKODE} fra CVR ...", file=sys.stderr)
    rækker, frasorteret = hent_alle(a.inkluder_bibrancher, a.alle_statusser)
    if not rækker:
        print("Ingen virksomheder fundet. Kør --probe for at tjekke feltstierne.")
        return 1
    skriv_csv(rækker, a.ud)
    opsummer(rækker, frasorteret, a.ud)
    return 0


if __name__ == "__main__":
    sys.exit(main())
