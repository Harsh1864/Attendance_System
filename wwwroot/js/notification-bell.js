/*
 * Keeps the unread badge on the toolbar bell up to date.
 * The bell markup (#notifBell / #notifBadge) is added by the admin and employee layouts.
 */
(function (window, $) {
    "use strict";

    var POLL_MS = 60000;

    function refresh() {
        $.ajax({ url: "/Notifications/UnreadCount", dataType: "json", headers: { "X-Requested-With": "XMLHttpRequest" } })
            .done(function (r) {
                var badge = $("#notifBadge");
                if (!badge.length) return;
                if (r.count > 0) {
                    badge.text(r.count > 99 ? "99+" : r.count).show();
                    $("#notifBell").attr("title", r.count + " unread notification(s)");
                } else {
                    badge.hide();
                    $("#notifBell").attr("title", "Notifications");
                }
            });
    }

    window.refreshNotificationBell = refresh;

    $(function () {
        // The toolbar is built on document ready, so wait a tick for it.
        setTimeout(refresh, 300);
        setInterval(function () { if (!document.hidden) refresh(); }, POLL_MS);
        document.addEventListener("visibilitychange", function () { if (!document.hidden) refresh(); });
    });
})(window, jQuery);
