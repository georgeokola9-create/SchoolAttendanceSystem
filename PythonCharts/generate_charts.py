import argparse
import json
import os
import sys

import matplotlib

matplotlib.use("Agg")
import matplotlib.patches as mpatches
import matplotlib.pyplot as plt
import numpy as np
from matplotlib.ticker import MaxNLocator

C_PRESENT = "#22c55e"
C_LATE = "#f59e0b"
C_ABSENT = "#ef4444"
C_BG = "#0f172a"
C_SURFACE = "#131f35"
C_BORDER = "#334155"
C_TEXT = "#e8edf5"
C_MUTED = "#7e95b8"
C_ACCENT = "#6366f1"


def apply_dark_theme(fig, axes):
    fig.patch.set_facecolor(C_BG)
    for ax in axes if isinstance(axes, list) else [axes]:
        ax.set_facecolor(C_SURFACE)
        ax.tick_params(colors=C_MUTED, labelsize=9, length=0)
        ax.xaxis.label.set_color(C_MUTED)
        ax.yaxis.label.set_color(C_MUTED)
        ax.title.set_color(C_TEXT)
        for spine in ax.spines.values():
            spine.set_edgecolor(C_BORDER)
        ax.set_axisbelow(True)
        ax.yaxis.grid(True, color=C_BORDER, linewidth=0.6, linestyle="--", alpha=0.5)
        ax.xaxis.grid(False)


def weekly_bar(data, out_dir):
    days = ["Mon", "Tue", "Wed", "Thu", "Fri"]
    present = normalize_series(data.get("weeklyPresent"), 5)
    late = normalize_series(data.get("weeklyLate"), 5)
    absent = normalize_series(data.get("weeklyAbsent"), 5)

    x = np.arange(len(days))
    width = 0.25
    fig, ax = plt.subplots(figsize=(7.2, 4.0))
    apply_dark_theme(fig, ax)

    bars = [
        ax.bar(x - width, present, width, label="Present", color=C_PRESENT, zorder=3, linewidth=0),
        ax.bar(x, late, width, label="Late", color=C_LATE, zorder=3, linewidth=0),
        ax.bar(x + width, absent, width, label="Absent", color=C_ABSENT, zorder=3, linewidth=0),
    ]

    max_value = max(present + late + absent + [1])
    y_top = max_value + max(1, max_value * 0.28)
    ax.set_ylim(0, y_top)

    for group in bars:
        for bar in group:
            height = bar.get_height()
            if height > 0:
                ax.text(
                    bar.get_x() + bar.get_width() / 2,
                    height + (y_top * 0.035),
                    str(int(height)),
                    ha="center",
                    va="bottom",
                    fontsize=8,
                    color=C_TEXT,
                    fontweight="bold",
                )

    ax.set_xticks(x)
    ax.set_xticklabels(days, fontsize=10, color=C_TEXT)
    ax.yaxis.set_major_locator(MaxNLocator(integer=True))
    ax.set_title("This Week - Daily Attendance", fontsize=12, fontweight="bold", pad=12)
    ax.set_ylabel("Teachers", fontsize=9)
    ax.legend(
        loc="upper center",
        bbox_to_anchor=(0.5, -0.16),
        framealpha=0,
        labelcolor=C_TEXT,
        fontsize=9,
        ncol=3,
        handlelength=1.2,
        handleheight=0.8,
        borderpad=0.6,
    )

    fig.subplots_adjust(left=0.09, right=0.98, top=0.84, bottom=0.25)
    fig.savefig(os.path.join(out_dir, "weekly_bar.png"), dpi=150, bbox_inches="tight")
    plt.close(fig)


def status_pie(data, out_dir):
    sizes = [
        int(data.get("todayPresent", 0) or 0),
        int(data.get("todayLate", 0) or 0),
        int(data.get("todayAbsent", 0) or 0),
    ]
    labels = ["Present", "Late", "Absent"]
    colors = [C_PRESENT, C_LATE, C_ABSENT]
    filtered = [(s, label, color) for s, label, color in zip(sizes, labels, colors) if s > 0]

    if not filtered:
        filtered = [(1, "No data", C_MUTED)]

    sizes, labels, colors = zip(*filtered)
    fig, ax = plt.subplots(figsize=(4.5, 4.5))
    apply_dark_theme(fig, ax)
    ax.set_facecolor(C_BG)

    _, _, autotexts = ax.pie(
        sizes,
        labels=None,
        colors=colors,
        autopct="%1.0f%%",
        pctdistance=0.78,
        startangle=90,
        wedgeprops={"linewidth": 3, "edgecolor": C_BG},
        counterclock=False,
    )

    for text in autotexts:
        text.set_fontsize(10)
        text.set_color(C_BG)
        text.set_fontweight("bold")

    centre = plt.Circle((0, 0), 0.52, fc=C_BG, linewidth=0)
    ax.add_patch(centre)

    total = sum(sizes) if labels[0] != "No data" else 0
    ax.text(0, 0.06, str(total), ha="center", va="center", fontsize=22, fontweight="bold", color=C_TEXT)
    ax.text(0, -0.22, "Total", ha="center", va="center", fontsize=9, color=C_MUTED)

    patches = [mpatches.Patch(color=color, label=label) for color, label in zip(colors, labels)]
    ax.legend(
        handles=patches,
        loc="lower center",
        bbox_to_anchor=(0.5, -0.08),
        ncol=min(3, len(patches)),
        framealpha=0,
        labelcolor=C_TEXT,
        fontsize=9,
        handlelength=1.2,
        handleheight=0.8,
    )

    ax.set_title("Today's Attendance", fontsize=12, fontweight="bold", pad=10, color=C_TEXT)
    fig.tight_layout(pad=1.2)
    fig.savefig(os.path.join(out_dir, "status_pie.png"), dpi=150, bbox_inches="tight", facecolor=C_BG)
    plt.close(fig)


def dept_bar(data, out_dir):
    departments = data.get("departments") or [{"name": "No data", "percentage": 0}]
    names = [str(d.get("name", "Unknown")) for d in departments]
    values = [float(d.get("percentage", 0) or 0) for d in departments]
    colors = [C_PRESENT if value >= 80 else C_LATE if value >= 60 else C_ABSENT for value in values]

    fig_height = max(3.4, 1.8 + (len(names) * 0.48))
    fig, ax = plt.subplots(figsize=(6.4, fig_height))
    apply_dark_theme(fig, ax)

    bars = ax.barh(names, values, color=colors, height=0.5, linewidth=0, zorder=3)
    for bar, value in zip(bars, values):
        inside_bar = value >= 14
        ax.text(
            value - 2 if inside_bar else min(value + 1.2, 98),
            bar.get_y() + bar.get_height() / 2,
            f"{value:g}%",
            va="center",
            ha="right" if inside_bar else "left",
            fontsize=10,
            color=C_TEXT,
            fontweight="bold",
        )

    ax.set_xlim(0, 100)
    ax.set_xlabel("Attendance %", fontsize=9)
    ax.tick_params(axis="y", labelsize=10, colors=C_TEXT)
    ax.xaxis.grid(False)
    ax.yaxis.grid(False)
    ax.set_title("Department Attendance This Month", fontsize=12, fontweight="bold", pad=12)
    ax.axvline(80, color=C_BORDER, linestyle="--", linewidth=1, alpha=0.7)

    fig.subplots_adjust(left=0.28, right=0.96, top=0.84, bottom=0.18)
    fig.savefig(os.path.join(out_dir, "dept_bar.png"), dpi=150, bbox_inches="tight")
    plt.close(fig)


def trend_line(data, out_dir):
    weeks = data.get("trendWeeks") or ["Wk 1", "Wk 2", "Wk 3", "Wk 4"]
    trend = [float(v or 0) for v in (data.get("trendValues") or [0, 0, 0, 0])]

    fig, ax = plt.subplots(figsize=(6, 3.2))
    apply_dark_theme(fig, ax)

    x = np.arange(len(weeks))
    ax.plot(
        x,
        trend,
        color=C_ACCENT,
        linewidth=2.5,
        marker="o",
        markersize=7,
        markerfacecolor=C_ACCENT,
        markeredgecolor=C_BG,
        markeredgewidth=2,
        zorder=4,
    )
    ax.fill_between(x, trend, alpha=0.12, color=C_ACCENT, zorder=2)

    for xi, yi in zip(x, trend):
        ax.text(xi, min(yi + 1.2, 104), f"{yi:g}%", ha="center", va="bottom", fontsize=9, color=C_TEXT, fontweight="bold")

    ax.set_xticks(x)
    ax.set_xticklabels(weeks, fontsize=10, color=C_TEXT)
    ax.set_ylim(max(0, min(trend) - 10), 105)
    ax.yaxis.set_major_formatter(plt.FuncFormatter(lambda value, _: f"{int(value)}%"))
    ax.set_title("4-Week Attendance Trend", fontsize=12, fontweight="bold", pad=12)

    fig.tight_layout(pad=1.4)
    fig.savefig(os.path.join(out_dir, "trend_line.png"), dpi=150, bbox_inches="tight")
    plt.close(fig)


def normalize_series(value, length):
    values = list(value or [])
    values = [int(v or 0) for v in values[:length]]
    return values + [0] * (length - len(values))


def read_data(args):
    if args.data_file:
        try:
            with open(args.data_file, "r", encoding="utf-8-sig") as file:
                return json.load(file)
        except json.JSONDecodeError:
            print("Warning: invalid JSON data file, using defaults", file=sys.stderr)
            return {}

    try:
        return json.loads(args.data or "{}")
    except json.JSONDecodeError:
        print("Warning: invalid JSON data, using defaults", file=sys.stderr)
        return {}


def main():
    parser = argparse.ArgumentParser(description="Generate attendance charts")
    parser.add_argument("--output", required=True, help="Output directory for PNG files")
    parser.add_argument("--data", required=False, default="{}", help="JSON attendance data")
    parser.add_argument("--data-file", required=False, help="Path to JSON attendance data")
    args = parser.parse_args()

    os.makedirs(args.output, exist_ok=True)
    data = read_data(args)

    weekly_bar(data, args.output)
    status_pie(data, args.output)
    dept_bar(data, args.output)
    trend_line(data, args.output)

    print("All charts generated successfully.")


if __name__ == "__main__":
    main()
