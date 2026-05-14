import argparse
import csv
import hashlib
import json
from pathlib import Path


ASSETS = {
    "SheetInfo": "resources__49__SheetInfo.txt",
    "GlossaryTerms": "resources__50__GlossaryTerms.txt",
    "UIElements": "resources__53__UIElements.txt",
    "Dialogs": "resources__55__Dialogs.txt",
    "Feats": "resources__58__Feats.txt",
    "QuestPoints": "resources__62__QuestPoints.txt",
}


def read_rows(path: Path):
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.reader(handle))


def write_rows(path: Path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerows(rows)


def copy_text(src: Path, dst: Path):
    dst.parent.mkdir(parents=True, exist_ok=True)
    dst.write_text(src.read_text(encoding="utf-8-sig"), encoding="utf-8")


def mirror_to_german(asset_name: str, src: Path, dst: Path):
    rows = read_rows(src)
    if not rows:
        copy_text(src, dst)
        return

    header = rows[0]
    source_name = "FR" if asset_name == "Dialogs" else "FRENCH"
    target_name = "DE" if asset_name == "Dialogs" else "GERMAN"

    if source_name not in header:
        copy_text(src, dst)
        return

    source_col = header.index(source_name)
    if target_name in header:
        target_col = header.index(target_name)
    else:
        header.append(target_name)
        target_col = len(header) - 1

    output = [header]
    for row in rows[1:]:
        if len(row) < len(header):
            row = row + [""] * (len(header) - len(row))

        if source_col < len(row) and row[source_col]:
            row[target_col] = row[source_col]

        output.append(row)

    write_rows(dst, output)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_game_build_guid(game_path: Path | None) -> str:
    if game_path is None:
        return ""

    boot_config = game_path / "Esoteric Ebb_Data" / "boot.config"
    if not boot_config.exists():
        return ""

    for line in boot_config.read_text(encoding="utf-8", errors="ignore").splitlines():
        if line.startswith("build-guid="):
            return line.split("=", 1)[1].strip()

    return ""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--game", type=Path, default=None)
    parser.add_argument("--status", type=Path, default=None)
    args = parser.parse_args()

    repo = args.repo.resolve()
    source = args.source.resolve()
    fr_dir = repo / "assets" / "translations" / "fr-columns"
    compat_dir = repo / "assets" / "translations" / "german-slot"

    for asset_name, file_name in ASSETS.items():
        src = source / file_name
        if not src.exists():
            raise FileNotFoundError(src)

        copy_text(src, fr_dir / f"{asset_name}.txt")
        mirror_to_german(asset_name, src, compat_dir / f"{asset_name}.txt")

    manifest = {
        "mod": "Esoteric Ebb - Traduction francaise",
        "language": "fr",
        "generated": "2026-05-14",
        "gameBuildGuid": read_game_build_guid(args.game.resolve() if args.game else None),
        "profiles": {},
    }

    for profile, directory in (("fr-columns", fr_dir), ("german-slot", compat_dir)):
        manifest["profiles"][profile] = [
            {
                "assetName": asset_name,
                "file": f"{asset_name}.txt",
                "bytes": (directory / f"{asset_name}.txt").stat().st_size,
                "sha256": sha256(directory / f"{asset_name}.txt"),
            }
            for asset_name in ASSETS
        ]

    manifest_path = repo / "assets" / "translations" / "manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    if args.status:
        status_target = repo / "docs" / "ETAT_LOCALISATION_FR.md"
        status_target.write_text(args.status.read_text(encoding="utf-8-sig"), encoding="utf-8")

    print(f"Updated translation profiles in {repo / 'assets' / 'translations'}")


if __name__ == "__main__":
    main()
