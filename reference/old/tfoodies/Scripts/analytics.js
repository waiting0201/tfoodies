(function (w, d) {
    //console.log("abc : " + d.referrer);
    $.post("https://www.tfoodies.com/Ajax/RecordLog", {
        url : w.location.href,
        referrerurl : d.referrer
    });
})(window, document);