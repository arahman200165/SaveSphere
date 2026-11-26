#!/usr/bin/env python3
"""Query the SaveSphere PRD (docs/SaveSphere_PRD_V0.0.html) without loading all 1.7 MB of it.

Usage:
  python scripts/prd.py index [--phase 01] [--module M05] [--priority P0]
  python scripts/prd.py req M01-R01 ARC-01 SEC-10     # full text of requirements (+ acceptance criteria)
  python scripts/prd.py id phase-01                   # text of any element by HTML id (section, module, phase...)
  python scripts/prd.py search "outbox"               # requirement IDs whose text mentions a term

Stdlib only. Run with `python -I` is not required, but never run it from an untrusted directory.
"""
from __future__ import annotations

import argparse
import re
import sys
from html.parser import HTMLParser
from pathlib import Path

PRD = Path(__file__).resolve().parent.parent / "docs" / "SaveSphere_PRD_V0.0.html"
VOID = {"br", "hr", "img", "meta", "link", "input", "area", "base", "col", "source", "wbr"}
BLOCK = {"p", "li", "h1", "h2", "h3", "h4", "h5", "tr", "div", "section", "article", "header", "summary", "details", "ol", "ul"}


class Extractor(HTMLParser):
    """Collects the element for each id/requirement as plain text."""

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.stack: list[tuple[str, list[str] | None]] = []  # (tag, buffer if this element is captured)
        self.captures: dict[str, list[str]] = {}
        self.attrs_of: dict[str, dict[str, str]] = {}
        self.order: list[str] = []
        self.skip = 0

    def handle_starttag(self, tag, attrs):
        if tag in VOID:
            return
        a = dict(attrs)
        if tag in ("script", "style", "button"):
            self.skip += 1
        buf = None
        key = a.get("id")
        if key and (a.get("class", "").split()[:1] in (["requirement"], ["module"], ["phase"]) or tag in ("section", "article")):
            buf = []
            self.captures[key] = buf
            self.attrs_of[key] = a
            self.order.append(key)
        self.stack.append((tag, buf))
        if tag in BLOCK:
            self._emit("\n")
        elif tag in ("td", "th"):
            self._emit(" | ")
        elif tag == "strong" or "priority-tag" in a.get("class", ""):
            self._emit(" ")

    def handle_endtag(self, tag):
        if tag in VOID:
            return
        # pop to the matching open tag (HTML here is well-formed, but be tolerant)
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][0] == tag:
                del self.stack[i:]
                break
        if tag in ("script", "style", "button") and self.skip:
            self.skip -= 1
        if tag in BLOCK:
            self._emit("\n")

    def handle_data(self, data):
        if not self.skip:
            self._emit(data)

    def _emit(self, text: str) -> None:
        for _, buf in self.stack:
            if buf is not None:
                buf.append(text)


def clean(parts: list[str]) -> str:
    t = "".join(parts)
    t = re.sub(r"[ \t]+", " ", t)
    t = re.sub(r" ?\n ?", "\n", t)
    return re.sub(r"\n{2,}", "\n", t).strip()


def load() -> Extractor:
    ex = Extractor()
    ex.feed(PRD.read_text(encoding="utf-8"))
    return ex


def reqs(ex: Extractor):
    for k in ex.order:
        a = ex.attrs_of[k]
        if "data-requirement" in a:
            yield k, a


def title_of(text: str) -> str:
    lines = text.splitlines()
    return lines[1] if len(lines) > 1 else lines[0]


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    pi = sub.add_parser("index")
    pi.add_argument("--phase")
    pi.add_argument("--module")
    pi.add_argument("--priority")
    pr = sub.add_parser("req")
    pr.add_argument("ids", nargs="+")
    pd = sub.add_parser("id")
    pd.add_argument("ids", nargs="+")
    ps = sub.add_parser("search")
    ps.add_argument("term")
    args = p.parse_args()

    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ex = load()

    if args.cmd == "index":
        for k, a in reqs(ex):
            text = clean(ex.captures[k])
            head = text.splitlines()[0]
            prio = re.search(r"\bP[0-3]\b", head)
            prio = prio.group(0) if prio else "?"
            if args.phase and a.get("data-phase") != args.phase:
                continue
            if args.module and a.get("data-module", "").upper() != args.module.upper():
                continue
            if args.priority and prio != args.priority.upper():
                continue
            ttl = re.sub(r"^\S+\s+(P[0-3]\s+)?", "", head)
            print(f"{a['data-requirement']:<10} ph{a.get('data-phase', '??')} {prio} {ttl}")
    elif args.cmd in ("req", "id"):
        rc = 0
        for i in args.ids:
            k = i.lower()
            if k not in ex.captures:
                print(f"[not found: {i}]", file=sys.stderr)
                rc = 1
                continue
            print(f"===== {i.upper()} =====")
            print(clean(ex.captures[k]))
            print()
        return rc
    elif args.cmd == "search":
        term = args.term.lower()
        hits = [a["data-requirement"] for k, a in reqs(ex) if term in clean(ex.captures[k]).lower()]
        print(f"{len(hits)} requirement(s) mention {args.term!r}:")
        print(", ".join(hits))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
