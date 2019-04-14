function setSnapClientMuted(clientMac, muted) {
    $.ajax({
            type: "POST",
            url: "api/snap/snapclientmute",
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            data: JSON.stringify({clientMac:clientMac,muted:muted})
        });
}

function mopidyTogglePlay(displayStateCallback) {
    $.ajax({
            type: "POST",
            url: "api/mopidy/togglepause",
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: (toggleResult) => {displayStateCallback(toggleResult.state);}
        });
}

function mopidyPlayUri(uri) {
    $.ajax({
            type: "POST",
            url: "api/mopidy/playepisode",
            data: JSON.stringify({uri: uri}),
            contentType: "application/json; charset=utf-8",
            dataType: "json"
        });
}

function reloadMe(container, viewSource) {
    setTimeout(function() {
        $(container).parent().load(viewSource);
    }, 500);
}