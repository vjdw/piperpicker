(() => {
    const maximumRetryCount = 10;
    const retryIntervalMilliseconds = 1000;
    const reconnectModal = document.getElementById('reconnect-modal');
    const showModal = (text) => { if (reconnectModal) { reconnectModal.style.display = 'block'; if (text) reconnectModal.innerText = text; } };
    const hideModal = () => { if (reconnectModal) reconnectModal.style.display = 'none'; };
    
    const startReconnectionProcess = () => {
        showModal("Reconnecting...");

        let isCanceled = false;

        (async () => {
            for (let i = 0; i < maximumRetryCount; i++) {
                showModal(`Reconnecting${'.'.repeat(i)}`);

                await new Promise(resolve => setTimeout(resolve, retryIntervalMilliseconds));

                if (isCanceled) {
                    return;
                }

                try {
                    const result = await Blazor.reconnect();
                    if (!result) {
                        // The server was reached, but the connection was rejected; reload the page.
                        location.reload();
                        return;
                    }

                    // Successfully reconnected to the server.
                    return;
                } catch {
                    // Didn't reach the server; try again.
                }
            }

            // Retried too many times; reload the page.
            location.reload();
        })();

        return {
            cancel: () => {
                isCanceled = true;
                hideModal();
            },
        };
    };

    let currentReconnectionProcess = null;

    Blazor.start({
        reconnectionHandler: {
            onConnectionDown: () => currentReconnectionProcess ??= startReconnectionProcess(),
            onConnectionUp: () => {
                currentReconnectionProcess?.cancel();
                currentReconnectionProcess = null;
            }
        }
    });
})();