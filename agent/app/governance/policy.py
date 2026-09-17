from __future__ import annotations

import re
from dataclasses import dataclass

# Patterns that must never override application rules
_INJECTION_PATTERNS = [
    re.compile(r"ignore\s+(all\s+)?(previous|prior|above)\s+instructions", re.I),
    re.compile(r"disregard\s+(your\s+)?(system|safety)\s+(prompt|rules)", re.I),
    re.compile(r"você\s+agora\s+é\s+(um\s+)?(admin|root|superuser)", re.I),
    re.compile(r"reveal\s+(your\s+)?(system\s+)?prompt", re.I),
    re.compile(r"exiba\s+(o\s+)?(prompt|chave|token|senha|secret)", re.I),
    re.compile(r"api[_-]?key\s*[:=]", re.I),
    re.compile(r"jwt_signing_key|openai_api_key|data_protection_key", re.I),
]

_SECRET_LIKE = re.compile(
    r"(?i)(password|senha|token|secret|api[_-]?key)\s*[:=]\s*\S+"
)

FORBIDDEN_ACTIONS = frozenset(
    {
        "delete_company",
        "wipe_inventory",
        "exfiltrate_secrets",
        "disable_auth",
        "publish_without_approval",
    }
)


@dataclass
class GovernanceResult:
    sanitized_message: str
    adversarial_detected: bool
    matched_patterns: list[str]
    blocked_actions: list[str]


def sanitize_and_scan(message: str) -> GovernanceResult:
    matched: list[str] = []
    for pattern in _INJECTION_PATTERNS:
        if pattern.search(message):
            matched.append(pattern.pattern)

    # Strip secret-like substrings from user text (never echo secrets)
    sanitized = _SECRET_LIKE.sub("[REDACTED]", message)
    blocked: list[str] = []
    if matched:
        blocked.extend(sorted(FORBIDDEN_ACTIONS))

    return GovernanceResult(
        sanitized_message=sanitized,
        adversarial_detected=bool(matched),
        matched_patterns=matched,
        blocked_actions=blocked,
    )


def may_execute(action: str, *, human_approved: bool, allow_destructive: bool) -> tuple[bool, str]:
    if action in FORBIDDEN_ACTIONS:
        return False, "action_forbidden_by_policy"
    if action.startswith("publish_") and not human_approved:
        return False, "requires_human_approval"
    if action.startswith("destructive_") and not allow_destructive:
        return False, "destructive_disabled"
    return True, "ok"
