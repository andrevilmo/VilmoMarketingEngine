#!/usr/bin/env python3
"""Estimativa simples de tendência de taxa de fallback (DevOps SCTEC)."""

from __future__ import annotations

RATES = [0.02, 0.03, 0.04, 0.08, 0.11, 0.15, 0.18]


def linear_slope(ys: list[float]) -> float:
    n = len(ys)
    xs = list(range(n))
    x_bar = sum(xs) / n
    y_bar = sum(ys) / n
    num = sum((x - x_bar) * (y - y_bar) for x, y in zip(xs, ys))
    den = sum((x - x_bar) ** 2 for x in xs) or 1.0
    return num / den


def main() -> None:
    slope = linear_slope(RATES)
    proj = RATES[-1] + 3 * slope
    risk = "high" if proj >= 0.25 else "medium" if proj >= 0.15 else "low"
    print(
        {
            "metric": "inventory_fallback_rate",
            "slope_per_day": round(slope, 4),
            "projection_d3": round(proj, 4),
            "risk": risk,
            "threshold_alert": 0.2,
        }
    )


if __name__ == "__main__":
    main()
