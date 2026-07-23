(function (global) {
    "use strict";

    const rankAssets = {
        basic: {
            gif: "/images/ranks/basic-rank.gif",
            png: "/images/ranks/basic-rank.png"
        },
        bronze: {
            gif: "/images/ranks/bronze-rank.gif",
            png: "/images/ranks/bronze-rank.png"
        },
        silver: {
            gif: "/images/ranks/silver-rank.gif",
            png: "/images/ranks/silver-rank.png"
        },
        gold: {
            gif: "/images/ranks/gold-rank.gif",
            png: "/images/ranks/gold-rank.png"
        },
        diamond: {
            gif: "/images/ranks/diamond-rank.gif",
            png: "/images/ranks/diamond-rank.png"
        }
    };

    function normalizeOtradeRankName(value) {
        const text = String(value || "")
            .trim()
            .toLowerCase();

        if (text.includes("diamond")) return "diamond";
        if (text.includes("gold")) return "gold";
        if (text.includes("silver")) return "silver";
        if (text.includes("bronze")) return "bronze";

        return "basic";
    }

    function prefersStaticRankImage() {
        return Boolean(
            global.matchMedia &&
            global.matchMedia("(prefers-reduced-motion: reduce)").matches
        );
    }

    function getOtradeRankAssetUrl(rankName, forceStatic) {
        const key = normalizeOtradeRankName(rankName);
        const asset = rankAssets[key] || rankAssets.basic;

        return forceStatic || prefersStaticRankImage()
            ? asset.png
            : asset.gif;
    }

    function setOtradeRankImage(target, rankName, options) {
        const image =
            typeof target === "string"
                ? document.getElementById(target)
                : target;

        if (!image) return;

        const settings = options || {};
        const key = normalizeOtradeRankName(rankName);
        const label = key.charAt(0).toUpperCase() + key.slice(1);

        image.dataset.rankName = key;
        image.alt = label + " Rank";
        image.decoding = "async";
        image.loading = settings.eager ? "eager" : "lazy";

        image.onerror = function () {
            image.onerror = null;
            image.src = getOtradeRankAssetUrl(key, true);
        };

        image.src = getOtradeRankAssetUrl(key, false);
    }

    global.normalizeOtradeRankName = normalizeOtradeRankName;
    global.getOtradeRankAssetUrl = getOtradeRankAssetUrl;
    global.setOtradeRankImage = setOtradeRankImage;
})(window);
