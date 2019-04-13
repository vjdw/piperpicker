function setSnapClientMuted(clientMac, muted, successCallback) {
    $.ajax({
            type: "POST",
            url: "api/snap/snapclientmute",
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            data: JSON.stringify({clientMac:clientMac,muted:muted}),
            success: successCallback()
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

function reloadMe() {
    setTimeout(function() {
        $("#snapclients-container").parent().load("control/?handler=SnapClientsView");
    }, 500);
}