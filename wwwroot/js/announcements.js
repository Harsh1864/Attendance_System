/*
 * Shows active announcements as dismissible banners at the top of employee pages.
 * A dismissed announcement stays hidden until an admin edits it (the key includes updatedAt).
 */
(function (window, $) {
    "use strict";

    var STORAGE_KEY = "dismissedAnnouncements";
    var MAX_BANNERS = 3;

    function dismissed() {
        try { return JSON.parse(window.localStorage.getItem(STORAGE_KEY) || "[]"); }
        catch (e) { return []; }
    }

    function remember(key) {
        var list = dismissed();
        if (list.indexOf(key) < 0) list.push(key);
        // Keep the list small.
        try { window.localStorage.setItem(STORAGE_KEY, JSON.stringify(list.slice(-100))); } catch (e) { /* storage full or blocked */ }
    }

    function keyFor(a) { return a.announcementId + ":" + a.updatedAt; }

    function esc(s) { return window.kendo ? kendo.htmlEncode(s) : String(s); }

    function render($target, items) {
        var hidden = dismissed();
        var visible = items.filter(function (a) { return hidden.indexOf(keyFor(a)) < 0; });
        if (!visible.length) { $target.empty(); return; }

        var html = visible.slice(0, MAX_BANNERS).map(function (a) {
            var important = a.priority === "Important";
            return "<div class='alert " + (important ? "alert-warning" : "alert-info") + " d-flex justify-content-between align-items-start mb-2' role='status'>" +
                "<div><strong>" + (important ? "Important: " : "") + esc(a.title) + "</strong>" +
                "<div class='small' style='white-space:pre-line'>" + esc(a.body) + "</div></div>" +
                "<button type='button' class='btn-close ms-3 js-dismiss-announcement' aria-label='Dismiss' data-key='" + esc(keyFor(a)) + "'></button>" +
                "</div>";
        }).join("");

        if (visible.length > MAX_BANNERS) {
            html += "<div class='small mb-2'><a href='/Announcements/Index'>See all " + visible.length + " announcements</a></div>";
        }
        $target.html(html);
    }

    $(function () {
        var $target = $("#announcementBanner");
        if (!$target.length) return;

        $.ajax({ url: "/Announcements/Active", dataType: "json", headers: { "X-Requested-With": "XMLHttpRequest" } })
            .done(function (items) {
                render($target, items);
                $target.on("click", ".js-dismiss-announcement", function () {
                    remember($(this).data("key"));
                    render($target, items);
                });
            });
    });
})(window, jQuery);
