const OTRADE_ACCESS_TOKEN_KEY =
    "token";

const OTRADE_USER_ID_KEY =
    "userId";

const OTRADE_REFRESH_BEFORE_SECONDS =
    60;

let otradeRefreshPromise =
    null;

let otradeRefreshTimer =
    null;

function getToken() {
    return localStorage.getItem(
        OTRADE_ACCESS_TOKEN_KEY
    );
}

function clearLocalAuth() {
    localStorage.removeItem(
        OTRADE_ACCESS_TOKEN_KEY
    );

    localStorage.removeItem(
        OTRADE_USER_ID_KEY
    );

    if (otradeRefreshTimer) {
        window.clearTimeout(
            otradeRefreshTimer
        );

        otradeRefreshTimer =
            null;
    }
}

function getTokenPayload(token) {
    try {
        if (!token) {
            return null;
        }

        const parts =
            token.split(".");

        if (parts.length !== 3) {
            return null;
        }

        let base64 =
            parts[1]
                .replace(/-/g, "+")
                .replace(/_/g, "/");

        while (
            base64.length % 4 !== 0
        ) {
            base64 += "=";
        }

        const json =
            decodeURIComponent(
                window
                    .atob(base64)
                    .split("")
                    .map(
                        character =>
                            "%" +
                            character
                                .charCodeAt(0)
                                .toString(16)
                                .padStart(2, "0")
                    )
                    .join("")
            );

        return JSON.parse(json);

    } catch {
        return null;
    }
}

function getTokenExpirationMilliseconds(
    token
) {
    const payload =
        getTokenPayload(
            token
        );

    if (
        !payload ||
        !payload.exp
    ) {
        return 0;
    }

    return Number(
        payload.exp
    ) * 1000;
}

function isTokenExpired(
    token,
    minimumValiditySeconds = 0
) {
    const expiresAt =
        getTokenExpirationMilliseconds(
            token
        );

    if (!expiresAt) {
        return true;
    }

    const requiredUntil =
        Date.now() +
        (
            Number(
                minimumValiditySeconds
            ) * 1000
        );

    return expiresAt <= requiredUntil;
}

async function refreshAccessToken() {
    if (otradeRefreshPromise) {
        return otradeRefreshPromise;
    }

    otradeRefreshPromise =
        (async function () {
            const response =
                await fetch(
                    "/api/auth/refresh",
                    {
                        method:
                            "POST",

                        credentials:
                            "same-origin",

                        cache:
                            "no-store",

                        headers: {
                            "X-Requested-With":
                                "XMLHttpRequest"
                        }
                    }
                );

            let result =
                null;

            try {
                result =
                    await response.json();
            } catch {
                result =
                    null;
            }

            if (
                !response.ok ||
                !result ||
                !result.success ||
                !result.data?.token
            ) {
                throw new Error(
                    result?.message ||
                    "Your login session has expired."
                );
            }

            const newToken =
                result.data.token;

            localStorage.setItem(
                OTRADE_ACCESS_TOKEN_KEY,
                newToken
            );

            if (result.data.userId) {
                localStorage.setItem(
                    OTRADE_USER_ID_KEY,
                    String(
                        result.data.userId
                    )
                );
            }

            scheduleAccessTokenRefresh();

            window.dispatchEvent(
                new CustomEvent(
                    "otrade:token-refreshed",
                    {
                        detail: {
                            token:
                                newToken
                        }
                    }
                )
            );

            return newToken;
        })();

    try {
        return await otradeRefreshPromise;

    } finally {
        otradeRefreshPromise =
            null;
    }
}

async function ensureFreshToken(
    minimumValiditySeconds =
        OTRADE_REFRESH_BEFORE_SECONDS
) {
    const currentToken =
        getToken();

    if (
        currentToken &&
        !isTokenExpired(
            currentToken,
            minimumValiditySeconds
        )
    ) {
        return currentToken;
    }

    return await refreshAccessToken();
}

function scheduleAccessTokenRefresh() {
    if (otradeRefreshTimer) {
        window.clearTimeout(
            otradeRefreshTimer
        );

        otradeRefreshTimer =
            null;
    }

    const token =
        getToken();

    if (!token) {
        return;
    }

    const expiresAt =
        getTokenExpirationMilliseconds(
            token
        );

    if (!expiresAt) {
        return;
    }

    /*
     * Token پنج دقیقه اعتبار دارد.
     * حدود یک دقیقه قبل از انقضا تمدید می‌شود.
     */
    const refreshAt =
        expiresAt -
        (
            OTRADE_REFRESH_BEFORE_SECONDS *
            1000
        );

    const delay =
        Math.max(
            refreshAt -
            Date.now(),
            1000
        );

    otradeRefreshTimer =
        window.setTimeout(
            async function () {
                try {
                    await refreshAccessToken();

                } catch {
                    await logout(
                        false
                    );
                }
            },
            delay
        );
}

async function checkAuth() {
    try {
        await ensureFreshToken(
            OTRADE_REFRESH_BEFORE_SECONDS
        );

        scheduleAccessTokenRefresh();

        return true;

    } catch {
        clearLocalAuth();

        window.location.href =
            "/auth/login";

        return false;
    }
}

async function logout(
    notifyServer = true
) {
    if (notifyServer) {
        try {
            await fetch(
                "/api/auth/logout",
                {
                    method:
                        "POST",

                    credentials:
                        "same-origin",

                    keepalive:
                        true,

                    headers: {
                        "X-Requested-With":
                            "XMLHttpRequest"
                    }
                }
            );
        } catch {
            /*
             * حتی اگر Server در دسترس نباشد،
             * اطلاعات محلی پاک می‌شود.
             */
        }
    }

    clearLocalAuth();

    window.location.href =
        "/auth/login";
}

window.addEventListener(
    "storage",
    function (event) {
        if (
            event.key ===
            OTRADE_ACCESS_TOKEN_KEY &&
            event.newValue
        ) {
            scheduleAccessTokenRefresh();
        }
    }
);