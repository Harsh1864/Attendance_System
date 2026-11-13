/*
 * Renders a month of DailyStatusModel entries as a calendar grid.
 * Used by the admin Reports page and the employee My Report page.
 *
 *   MonthCalendar.render($("#target"), days);
 */
(function (window, $) {
    "use strict";

    var KINDS = {
        Present:   { label: "Present",    color: "#d4edda", text: "#155724" },
        Late:      { label: "Late in",    color: "#fff3cd", text: "#856404" },
        EarlyOut:  { label: "Early out",  color: "#ffe5cc", text: "#8a4b00" },
        LateEarly: { label: "Late + early", color: "#ffd6a5", text: "#7a3e00" },
        Absent:    { label: "Absent",     color: "#f8d7da", text: "#721c24" },
        Leave:     { label: "Leave",      color: "#d1ecf1", text: "#0c5460" },
        Holiday:   { label: "Holiday",    color: "#e2d9f3", text: "#4a2c7a" },
        Weekend:   { label: "Weekend",    color: "#f1f3f5", text: "#868e96" },
        NoRecord:  { label: "No record",  color: "#ffffff", text: "#adb5bd" },
        Future:    { label: "",           color: "#ffffff", text: "#dee2e6" }
    };

    var WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    function esc(s) {
        return window.kendo ? kendo.htmlEncode(s == null ? "" : String(s)) : String(s == null ? "" : s);
    }

    function parse(d) {
        return d instanceof Date ? d : (window.kendo ? kendo.parseDate(d) : new Date(d));
    }

    function tooltip(day) {
        var parts = [KINDS[day.kind] ? KINDS[day.kind].label : day.kind];
        if (day.clockIn) parts.push("In " + day.clockIn + (day.clockOut ? ", out " + day.clockOut : ""));
        if (day.hours) parts.push(day.hours + " h");
        if (day.workType) parts.push(day.workType);
        if (day.detail) parts.push(day.detail);
        return parts.filter(Boolean).join(" · ");
    }

    function cell(day) {
        var k = KINDS[day.kind] || KINDS.NoRecord;
        var date = parse(day.date);
        var sub = "";
        if (day.clockIn) sub = day.clockIn + (day.clockOut ? "–" + day.clockOut : "");
        else if (day.kind === "Holiday" || day.kind === "Leave") sub = day.detail || k.label;
        else if (day.kind === "Absent") sub = "Absent";

        return "<td class='mc-day' title='" + esc(tooltip(day)) + "' style='background:" + k.color + ";color:" + k.text + "'>" +
            "<div class='mc-num'>" + date.getDate() + "</div>" +
            "<div class='mc-sub'>" + esc(sub) + "</div></td>";
    }

    function legend() {
        return "<div class='calendar-legend'>" + Object.keys(KINDS)
            .filter(function (k) { return k !== "Future"; })
            .map(function (k) {
                return "<span style='--swatch:" + KINDS[k].color + ";border:0'>" + KINDS[k].label + "</span>";
            }).join("") + "</div>";
    }

    function render($target, days) {
        if (!days || !days.length) {
            $target.html("<div class='empty-state'>No data for this month.</div>");
            return;
        }

        var first = parse(days[0].date);
        // Monday-based column of the first day (0 = Monday).
        var offset = (first.getDay() + 6) % 7;

        var html = "<table class='mc-table'><thead><tr>" +
            WEEKDAYS.map(function (w) { return "<th>" + w + "</th>"; }).join("") +
            "</tr></thead><tbody><tr>";

        for (var i = 0; i < offset; i++) html += "<td class='mc-empty'></td>";

        days.forEach(function (day, idx) {
            html += cell(day);
            if ((offset + idx + 1) % 7 === 0 && idx < days.length - 1) html += "</tr><tr>";
        });

        var trailing = (7 - (offset + days.length) % 7) % 7;
        for (var j = 0; j < trailing; j++) html += "<td class='mc-empty'></td>";

        html += "</tr></tbody></table>" + legend();
        $target.html(html);
    }

    window.MonthCalendar = { render: render, kinds: KINDS };
})(window, jQuery);
