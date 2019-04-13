function setSnapClientMuted(clientMac, muted) {
    $.ajax({
            type: "POST",
            url: "api/Snap/SnapClientMute",
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