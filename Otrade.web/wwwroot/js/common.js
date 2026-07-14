function showNotification(
    message,
    type = "success",
    title = null,
    duration = 4000) {
    const container =
        document.getElementById(
            "notificationContainer");

    if (!container)
        return;

    const notification =
        document.createElement("div");

    notification.className =
        `notification ${type}`;

    notification.innerHTML = `
        <div class="notification-title">
            ${title || getNotificationTitle(type)}
        </div>

        <div class="notification-message">
            ${message}
        </div>
    `;

    container.appendChild(notification);

    setTimeout(() => {

        notification.style.animation =
            "notificationHide .35s ease forwards";

        setTimeout(() => {
            notification.remove();
        }, 350);

    }, duration);
}

function getNotificationTitle(type) {
    switch (type) {
        case "error":
            return "Error";

        case "warning":
            return "Warning";

        case "success":
            return "Success";

        default:
            return "Notification";
    }
}

function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
}

function showConfirm(message, title = "Confirm Action") {
    return new Promise((resolve) => {
        let existing = document.getElementById("confirmOverlay");
        if (existing) existing.remove();

        const overlay = document.createElement("div");
        overlay.id = "confirmOverlay";
        overlay.className = "confirm-overlay";

        overlay.innerHTML = `
            <div class="confirm-box">
                <h4>${title}</h4>
                <p>${message}</p>

                <div class="confirm-actions">
                    <button type="button" class="confirm-btn cancel">
                        Cancel
                    </button>

                    <button type="button" class="confirm-btn confirm">
                        Confirm
                    </button>
                </div>
            </div>
        `;

        document.body.appendChild(overlay);

        const cancelBtn = overlay.querySelector(".confirm-btn.cancel");
        const confirmBtn = overlay.querySelector(".confirm-btn.confirm");

        cancelBtn.addEventListener("click", () => {
            overlay.remove();
            resolve(false);
        });

        confirmBtn.addEventListener("click", () => {
            overlay.remove();
            resolve(true);
        });

        overlay.addEventListener("click", (e) => {
            if (e.target === overlay) {
                overlay.remove();
                resolve(false);
            }
        });
    });
}
