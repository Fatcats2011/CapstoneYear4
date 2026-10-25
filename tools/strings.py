"""Writes Assets/Resources/Localization/strings.csv from tools/strings_en.py (the keys, notes and English) and
tools/strings_tr.py (the translations, when present). A key's translation missing from strings_tr.py is kept from
the CSV if it was there, else filled with the English (the CSV must have every cell). Run: python tools/strings.py"""
import csv
import importlib.util
import os

HERE = os.path.dirname(os.path.abspath(__file__))
CSV = os.path.join(HERE, '..', 'Assets', 'Resources', 'Localization', 'strings.csv')
LOCALES = ["en", "fr", "de", "it", "nl", "es", "es-419", "pt", "pt-BR", "pl", "tr", "cs", "ro", "hu", "sv", "da"]


def load(name):
    path = os.path.join(HERE, name)
    if not os.path.exists(path):
        return None
    spec = importlib.util.spec_from_file_location(name[:-3], path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    english = load('strings_en.py').ROWS
    translations = getattr(load('strings_tr.py'), 'TR', {}) if load('strings_tr.py') else {}

    existing = {}
    if os.path.exists(CSV):
        with open(CSV, encoding='utf-8', newline='') as f:
            for row in csv.DictReader(f):
                existing[row['key']] = row

    with open(CSV, 'w', encoding='utf-8', newline='') as f:
        out = csv.writer(f, lineterminator='\n')
        out.writerow(['key', 'note'] + LOCALES + ['reviewed'])
        for key, note, en in english:
            cells = [key, note, en]
            for locale in LOCALES[1:]:
                text = translations.get(key, {}).get(locale)
                if not text:
                    old = existing.get(key, {}).get(locale)
                    text = old if old and old != existing.get(key, {}).get('en') else None
                cells.append(text or en)
            cells.append(existing.get(key, {}).get('reviewed', ''))
            out.writerow(cells)
    print('wrote', len(english), 'keys')


if __name__ == '__main__':
    main()
