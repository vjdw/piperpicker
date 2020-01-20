function setSnapClientMuted(e, clientMac, muted) {
    $(e).toggleClass("spinner");
    $.ajax({
            type: "POST",
            url: "api/snap/snapclientmute",
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            data: JSON.stringify({clientMac:clientMac,muted:muted})
        });
}

function setSnapClientGlobalVolume(e, percentagePointChange) {
    $(".current-volume").addClass("spinner");
    $.ajax({
        type: "POST",
        url: "api/snap/snapclientglobalvolume",
        contentType: "application/json; charset=utf-8",
        async: true,
        dataType: "json",
        data: JSON.stringify({PercentagePointChange:percentagePointChange})
    });
}

function mopidyTogglePlay(displayStateCallback) {
    $.ajax({
            type: "POST",
            url: "api/mopidy/togglepause",
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: (toggleResult) => { displayStateCallback(toggleResult.state); }
        });
}

function mopidyPlayUri(uri, displayStateCallback) {
    $.ajax({
            type: "POST",
            url: "api/mopidy/playepisode",
            data: JSON.stringify({uri: uri}),
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            success: (toggleResult) => { displayStateCallback && displayStateCallback(toggleResult.state);}
        });
}

function reloadMe(container, viewSource) {
    setTimeout(function() {
        $(container).parent().load(viewSource)
    }, 500);
}

function lightingColour() {
    $.ajax({
        type: "POST",
        url: "api/lighting/colour",
        data: JSON.stringify({ red: 30, green: 8, blue: 0, white: 0 }),
        contentType: "application/json; charset=utf-8",
        dataType: "json"
        //,success: (toggleResult) => { displayStateCallback && displayStateCallback(toggleResult.state); }
    });
}