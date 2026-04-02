/**
 * Компонент Drag & Drop
 */

export class DropZone {
    constructor(dropZoneId, inputId, infoId, onFilesChanged) {
        this.dropZone = document.getElementById(dropZoneId);
        this.input = document.getElementById(inputId);
        this.info = document.getElementById(infoId);
        this.onFilesChanged = onFilesChanged;
        this.picking = false;
        this.init();
    }

    init() {
        if (!this.dropZone || !this.input) return;

        // Click to open file picker
        this.dropZone.addEventListener("click", (e) => {
            e.preventDefault();
            e.stopPropagation();
            this.openPicker();
        });

        // Keyboard support
        this.dropZone.addEventListener("keydown", (e) => {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                this.openPicker();
            }
        });

        // File input change
        this.input.addEventListener("change", () => {
            if (this.onFilesChanged) {
                this.onFilesChanged();
            }
        });

        // Drag & drop events
        this.dropZone.addEventListener("dragover", (e) => {
            e.preventDefault();
            this.dropZone.classList.add("is-over");
        });

        this.dropZone.addEventListener("dragleave", () => {
            this.dropZone.classList.remove("is-over");
        });

        this.dropZone.addEventListener("drop", (e) => {
            e.preventDefault();
            e.stopPropagation();
            this.dropZone.classList.remove("is-over");
            this.handleDrop(e);
        });
    }

    openPicker() {
        if (this.picking) return;
        this.picking = true;
        this.input.click();
        setTimeout(() => { this.picking = false; }, 500);
    }

    handleDrop(e) {
        // Override in subclass for custom behavior
    }

    updateInfo(text) {
        if (this.info) {
            this.info.textContent = text;
        }
    }
}

/**
 * Single file drop zone
 */
export class SingleFileDropZone extends DropZone {
    handleDrop(e) {
        const file = Array.from(e.dataTransfer?.files || [])
            .find(f => f?.size > 0 && (f.type || "").startsWith("image/"));
        
        if (!file) return;
        
        const dt = new DataTransfer();
        dt.items.add(file);
        this.input.files = dt.files;
        
        this.updateInfo(`Обрано: ${file.name}`);
    }
}

/**
 * Multiple files drop zone
 */
export class MultipleFilesDropZone extends DropZone {
    constructor(dropZoneId, inputId, infoId, onFilesChanged, droppedFilesStore) {
        super(dropZoneId, inputId, infoId, onFilesChanged);
        this.droppedFilesStore = droppedFilesStore; // reference to state.droppedFiles
    }

    handleDrop(e) {
        const files = Array.from(e.dataTransfer?.files || [])
            .filter(f => f?.size > 0 && (f.type || "").startsWith("image/"));
        
        if (this.droppedFilesStore) {
            // Store dropped files in external state
            this.droppedFilesStore.length = 0;
            files.forEach(f => this.droppedFilesStore.push(f));
        }
        
        this.input.value = "";
        
        if (this.onFilesChanged) {
            this.onFilesChanged();
        }
    }
}
