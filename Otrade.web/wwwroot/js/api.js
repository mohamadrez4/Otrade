async function parseApiResponse(
    response
) {
    if (
        response.status === 204
    ) {
        return null;
    }

    const contentType =
        response.headers.get(
            "content-type"
        ) || "";

    if (
        contentType.includes(
            "application/json"
        )
    ) {
        return await response.json();
    }

    const text =
        await response.text();

    return {
        success:
            response.ok,

        message:
            text ||
            (
                response.ok
                    ? "Operation completed."
                    : "Request failed."
            ),

        data:
            null
    };
}

function buildApiRequestOptions(
    options,
    accessToken
) {
    const requestOptions = {
        ...options,

        credentials:
            "same-origin",

        cache:
            options.cache ||
            "no-store"
    };

    const headers =
        new Headers(
            options.headers ||
            {}
        );

    headers.set(
        "Authorization",
        `Bearer ${accessToken}`
    );

    headers.set(
        "X-Requested-With",
        "XMLHttpRequest"
    );

    const isFormData =
        options.body instanceof
        FormData;

    if (
        options.body != null &&
        !isFormData &&
        !headers.has(
            "Content-Type"
        )
    ) {
        headers.set(
            "Content-Type",
            "application/json"
        );
    }

    requestOptions.headers =
        headers;

    return requestOptions;
}

async function api(
    url,
    options = {}
) {
    let accessToken;

    try {
        accessToken =
            await ensureFreshToken(
                60
            );

    } catch {
        await logout(
            false
        );

        return;
    }

    let response =
        await fetch(
            url,
            buildApiRequestOptions(
                options,
                accessToken
            )
        );

    /*
     * ممکن است Token بین بررسی Client و رسیدن
     * درخواست به Server منقضی شده باشد.
     *
     * فقط یک بار Refresh و Retry انجام می‌شود.
     */
    if (
        response.status === 401
    ) {
        try {
            accessToken =
                await refreshAccessToken();

            response =
                await fetch(
                    url,
                    buildApiRequestOptions(
                        options,
                        accessToken
                    )
                );

        } catch {
            await logout(
                false
            );

            return;
        }
    }

    const result =
        await parseApiResponse(
            response
        );

    if (
        response.status === 401
    ) {
        await logout(
            false
        );

        return;
    }

    /*
     * 403 به‌معنای نداشتن Permission است،
     * نه منقضی‌شدن Login؛ بنابراین Logout نمی‌کنیم.
     */
    return result;
}