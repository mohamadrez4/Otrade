function getToken() {
    return localStorage.getItem("token");
}

function logout() {
    localStorage.removeItem("token");
    window.location.href = "/auth/login";
}

function isTokenExpired(token) {
    try {
        const payload = JSON.parse(atob(token.split('.')[1]));

        if (!payload.exp)
            return true;

        return payload.exp * 1000 < Date.now();
    }
    catch {
        return true;
    }
}

function checkAuth() {
    const token = getToken();

    if (!token) {
        logout();
        return false;
    }

    if (isTokenExpired(token)) {
        logout();
        return false;
    }

    return true;
}
