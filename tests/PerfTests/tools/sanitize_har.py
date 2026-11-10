"""Writes a shareable copy of a raw journey HAR: only SCTI's own requests, no cookies, static file bodies removed
and the Google Maps key redacted.

Usage: python3 tests/PerfTests/tools/sanitize_har.py <raw.har> <output.har>
"""
import json
import sys
from urllib.parse import urlsplit

SITE_HOSTS = {"insure.scti.co.nz", "scti.co.nz"}
REDACTED_JSON_KEYS = {"googleMapsApiKey"}


def is_site_request(entry):
    return urlsplit(entry["request"]["url"]).hostname in SITE_HOSTS


def is_api_call(entry):
    return "/api/" in urlsplit(entry["request"]["url"]).path


def without_cookies(headers):
    return [h for h in headers if h["name"].lower() not in ("cookie", "set-cookie")]


def redact(text):
    try:
        body = json.loads(text)
    except ValueError:
        return text
    if isinstance(body, dict):
        for key in REDACTED_JSON_KEYS & body.keys():
            body[key] = "<redacted>"
    return json.dumps(body, separators=(",", ":"))


def sanitize(entry):
    request, response = entry["request"], entry["response"]
    request["headers"] = without_cookies(request["headers"])
    request["cookies"] = []
    response["headers"] = without_cookies(response["headers"])
    response["cookies"] = []
    content = response.get("content", {})
    if is_api_call(entry) and "text" in content:
        content["text"] = redact(content["text"])
    else:
        # Pages, scripts and images are kept as requests only; their bodies would add megabytes.
        content.pop("text", None)
        content.pop("encoding", None)
        content["comment"] = "body removed"
    return entry


def main(raw_path, output_path):
    with open(raw_path, encoding="utf-8") as file:
        har = json.load(file)
    log = har["log"]
    log["entries"] = [sanitize(entry) for entry in log["entries"] if is_site_request(entry)]
    log.pop("pages", None)
    for entry in log["entries"]:
        entry.pop("pageref", None)
    with open(output_path, "w", encoding="utf-8") as file:
        json.dump(har, file, indent=1)
    print(f"{len(log['entries'])} site requests written to {output_path}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
