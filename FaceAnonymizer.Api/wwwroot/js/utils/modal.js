/**
 * Управління модальним вікном для перегляду зображень
 */

export class ImageModal {
    constructor(modalId = "imageModal", imageId = "modalImage", closeId = "modalClose") {
        this.modal = document.getElementById(modalId);
        this.image = document.getElementById(imageId);
        this.closeBtn = document.getElementById(closeId);
        this.init();
    }

    init() {
        if (!this.modal || !this.closeBtn) return;

        this.closeBtn.addEventListener("click", () => this.close());
        this.modal.addEventListener("click", (e) => {
            if (e.target === this.modal) this.close();
        });
        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") this.close();
        });
    }

    open(src) {
        if (!this.modal || !this.image) return;
        this.image.src = src;
        this.modal.classList.add("is-active");
        document.body.style.overflow = "hidden";
    }

    close() {
        if (!this.modal) return;
        this.modal.classList.remove("is-active");
        document.body.style.overflow = "";
    }

    /**
     * Додати можливість кліку на зображення для відкриття модалки
     */
    static makeClickable(imageElement, srcGetter) {
        if (!imageElement) return;
        imageElement.classList.add("clickable");
        imageElement.addEventListener("click", () => {
            const src = typeof srcGetter === "function" ? srcGetter() : imageElement.src;
            if (src && !imageElement.classList.contains("is-hidden")) {
                window.imageModal?.open(src);
            }
        });
    }
}
