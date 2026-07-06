async function api(url, options = {}) {
    const token = localStorage.getItem("token");

    if (!token) {
        window.location.href = "/auth/login";
        return;
    }

    const isFormData = options.body instanceof FormData;

    options.headers = {
        ...(options.headers || {}),
        "Authorization": `Bearer ${token}`
    };

    if (!isFormData) {
        options.headers["Content-Type"] = "application/json";
    }

    const response = await fetch(url, options);

    if (response.status === 401 || response.status === 403) {
        localStorage.removeItem("token");
        window.location.href = "/auth/login";
        return;
    }

    return await response.json();
}