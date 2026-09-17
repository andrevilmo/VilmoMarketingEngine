#!/usr/bin/env python3
"""Estimativa simples de tendência de falha a partir de série retries/100."""

from __future__ import annotations

RETRIES_PER_100 = [2, 3, 5, 8, 12, 15, 18]
FINAL_FAILURES = [0, 0, 1, 1, 2, 2, 3]


def slope(xs: list[int], ys: list[int]) -> float:
    n = len(xs)
    mean_x = sum(xs) / n
    mean_y = sum(ys) / n
    num = sum((x - mean_x) * (y - mean_y) for x, y in zip(xs, ys))
    den = sum((x - mean_x) ** 2 for x in xs) or 1.0
    return num / den


def main() -> None:
    days = list(range(len(RETRIES_PER_100)))
    m = slope(days, RETRIES_PER_100)
    next_retries = RETRIES_PER_100[-1] + m
    # Heurística: cada +10 retries/100 ≈ +5pp falha
    fail_rate = FINAL_FAILURES[-1] / 100
    projected = min(0.9, max(0.0, fail_rate + (m / 10) * 0.05))
    print(
        {
            "retry_slope_per_day": round(m, 3),
            "projected_retries_next_day": round(next_retries, 2),
            "projected_failure_probability": round(projected, 3),
            "interpretation": "tendência de alta — acionar alerta low-code se p>0.2",
        }
    )


if __name__ == "__main__":
    main()
