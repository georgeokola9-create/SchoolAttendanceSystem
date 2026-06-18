const REFRESH_INTERVAL = 30000;
const AVATAR_COLORS = ["#6366f1", "#3b82f6", "#22c55e", "#f59e0b", "#ef4444", "#8b5cf6", "#06b6d4"];

function startClock() {
    const el = document.getElementById("liveClock");
    if (!el) return;

    const tick = () => {
        const now = new Date();
        const hours = now.getHours();
        const minutes = String(now.getMinutes()).padStart(2, "0");
        const seconds = String(now.getSeconds()).padStart(2, "0");
        const suffix = hours >= 12 ? "PM" : "AM";
        const hour12 = hours % 12 || 12;
        el.textContent = `${hour12}:${minutes}:${seconds} ${suffix}`;
    };

    tick();
    setInterval(tick, 1000);
}

function initLiveRefresh() {
    if (!document.getElementById("attTableBody")) return;
    setInterval(refreshTable, REFRESH_INTERVAL);
}

function refreshTable() {
    fetch("/Attendance/LiveData", {
        headers: { "Accept": "application/json" }
    })
        .then(response => {
            if (!response.ok) throw new Error("Refresh failed");
            return response.json();
        })
        .then(data => {
            updateStatCards(data);
            updateTable(data.rows, data.pastAbsent);
            applySearchFilter();
            flashRefresh();
        })
        .catch(() => {
            // Silent refresh failure keeps the dashboard usable during restarts.
        });
}

function updateStatCards(data) {
    setText("statPresent", data.presentCount);
    setText("statLate", data.lateCount);
    setText("statAbsent", data.absentCount);
    setText("statPercentage", `${data.percentage}%`);
    setText("statAbsentLabel", data.pastAbsent ? "Absent Today" : `Absent (after ${data.absentThreshold || "10:00 AM"})`);

    const bar = document.getElementById("attendanceBar");
    if (bar) bar.style.width = `${data.percentage}%`;
}

function updateTable(rows, pastAbsent) {
    const tbody = document.getElementById("attTableBody");
    if (!tbody || !Array.isArray(rows)) return;

    tbody.innerHTML = rows.map(row => {
        const initials = getInitials(row.teacherName);
        const badgeClass = getBadgeClass(row.status, pastAbsent);
        const badgeLabel = getStatusLabel(row.status, pastAbsent);

        return `
            <tr>
                <td>
                    <div class="teacher-cell">
                        <span class="teacher-avatar" style="background:${avatarColor(row.teacherName)}">${escHtml(initials)}</span>
                        <span>
                            <span class="teacher-name">${escHtml(row.teacherName)}</span>
                            <span class="teacher-tsc">${escHtml(row.tscNumber)}</span>
                        </span>
                    </div>
                </td>
                <td><span class="dept-tag">${escHtml(row.department || "Unassigned")}</span></td>
                <td class="time-cell">${escHtml(row.checkIn)}</td>
                <td><span class="badge ${badgeClass}">${escHtml(badgeLabel)}</span></td>
                <td>
                    <a href="/Attendance/History/${encodeURIComponent(row.teacherId)}" class="btn btn-ghost btn-sm">
                        <i class="ti ti-clock-history"></i>
                        History
                    </a>
                </td>
            </tr>`;
    }).join("");
}

function initSearch() {
    const input = document.getElementById("tableSearch");
    if (!input) return;
    input.addEventListener("input", applySearchFilter);
}

function applySearchFilter() {
    const input = document.getElementById("tableSearch");
    const tbody = document.getElementById("attTableBody");
    if (!input || !tbody) return;

    const query = input.value.trim().toLowerCase();
    tbody.querySelectorAll("tr").forEach(row => {
        row.style.display = row.textContent.toLowerCase().includes(query) ? "" : "none";
    });
}

function getBadgeClass(status, pastAbsent) {
    if (status === "Absent" && !pastAbsent) return "badge-pending";
    return {
        Present: "badge-present",
        Late: "badge-late",
        Absent: "badge-absent"
    }[status] || "badge-pending";
}

function getStatusLabel(status, pastAbsent) {
    return status === "Absent" && !pastAbsent ? "Not yet in" : status;
}

function getInitials(name) {
    const initials = String(name || "T")
        .split(/\s+/)
        .filter(Boolean)
        .map(part => part[0])
        .join("")
        .toUpperCase();

    return (initials || "T").slice(0, 2);
}

function avatarColor(name) {
    let hash = 0;
    for (const char of String(name || "")) {
        hash = char.charCodeAt(0) + ((hash << 5) - hash);
    }

    return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

function flashRefresh() {
    const dot = document.getElementById("liveDot");
    if (!dot) return;
    dot.style.opacity = "0.35";
    setTimeout(() => {
        dot.style.opacity = "1";
    }, 180);
}

function setText(id, value) {
    const el = document.getElementById(id);
    if (el) el.textContent = value;
}

function escHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}

document.addEventListener("DOMContentLoaded", () => {
    startClock();
    initSearch();
    initLiveRefresh();

    document.querySelectorAll(".stat-card").forEach((card, index) => {
        card.style.opacity = "0";
        card.style.transform = "translateY(10px)";
        setTimeout(() => {
            card.style.transition = "opacity 0.25s ease, transform 0.25s ease";
            card.style.opacity = "1";
            card.style.transform = "translateY(0)";
        }, index * 60);
    });
});
