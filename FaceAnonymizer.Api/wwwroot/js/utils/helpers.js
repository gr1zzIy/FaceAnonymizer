/**
 * Утиліти загального призначення
 */

export const utils = {
    /**
     * Екранування HTML
     */
    escapeHtml(str) {
        return String(str ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    },

    /**
     * Унікальні файли (за назвою, розміром та датою)
     */
    uniqueFiles(files) {
        const map = new Map();
        for (const f of files) {
            map.set(`${f.name}|${f.size}|${f.lastModified}`, f);
        }
        return Array.from(map.values());
    },

    /**
     * Форматування байтів
     */
    formatBytes(bytes, decimals = 2) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    },

    /**
     * Затримка (Promise-based)
     */
    delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
};
