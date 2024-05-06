(() => {
    const maximumRetryCount = 10;
    const retryIntervalMilliseconds = 1000;
    const reconnectModal = document.getElementById('reconnect-modal');

    const startReconnectionProcess = () => {
        reconnectModal.style.display = 'block';

        let isCanceled = false;

        (async () => {
            for (let i = 0; i < maximumRetryCount; i++) {
                reconnectModal.innerText = `Reconnecting${'.'.repeat(i)}`;

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
                reconnectModal.style.display = 'none';
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