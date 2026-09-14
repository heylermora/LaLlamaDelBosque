(function () {
    "use strict";

    const minimumOptionsForSearch = 7;
    const collator = new Intl.Collator("es", {
        sensitivity: "base",
        numeric: true,
        ignorePunctuation: true
    });

    function isPlaceholder(option, index) {
        if (index !== 0) {
            return false;
        }

        const text = option.textContent.trim().toLocaleLowerCase("es");
        return option.disabled
            || option.value === ""
            || option.value === "-1"
            || text.startsWith("seleccione");
    }

    function sortContainer(container) {
        const options = Array.from(container.children).filter(element => element.tagName === "OPTION");
        if (options.length < 2) {
            return;
        }

        const placeholder = isPlaceholder(options[0], 0) ? options.shift() : null;
        const sortedOptions = [...options].sort((left, right) =>
            collator.compare(left.textContent.trim(), right.textContent.trim()));
        const expectedOptions = placeholder ? [placeholder, ...sortedOptions] : sortedOptions;
        const currentOptions = Array.from(container.children).filter(element => element.tagName === "OPTION");

        if (expectedOptions.some((option, index) => option !== currentOptions[index])) {
            expectedOptions.forEach(option => container.appendChild(option));
        }
    }

    function sortOptions(select) {
        sortContainer(select);
        Array.from(select.children)
            .filter(element => element.tagName === "OPTGROUP")
            .forEach(sortContainer);
    }

    function searchableOptions(select) {
        return Array.from(select.options).filter((option, index) => !isPlaceholder(option, index));
    }

    function updateSearchVisibility(select, searchContainer) {
        const shouldShow = searchableOptions(select).length >= minimumOptionsForSearch && !select.disabled;
        searchContainer.hidden = !shouldShow;

        if (!shouldShow) {
            const input = searchContainer.querySelector("input");
            input.value = "";
            searchableOptions(select).forEach(option => option.hidden = false);
        }
    }

    function addSearch(select) {
        if (select.dataset.searchableSelectReady === "true" || select.multiple) {
            return;
        }

        select.dataset.searchableSelectReady = "true";
        if (!select.id) {
            select.id = `ordered-select-${document.querySelectorAll("select[data-searchable-select-ready]").length}`;
        }

        const searchContainer = document.createElement("div");
        searchContainer.className = "select-search mb-2";
        searchContainer.innerHTML = `
            <label class="visually-hidden" for="${select.id}-search">Buscar en las opciones</label>
            <span class="material-icons select-search__icon" aria-hidden="true">search</span>
            <input id="${select.id}-search" type="search" class="form-control select-search__input"
                   placeholder="Buscar una opción..." autocomplete="off" aria-controls="${select.id}">
        `;

        select.before(searchContainer);
        const input = searchContainer.querySelector("input");
        input.addEventListener("input", function () {
            const query = this.value.trim().toLocaleLowerCase("es");
            searchableOptions(select).forEach(option => {
                option.hidden = query !== "" && !option.textContent.toLocaleLowerCase("es").includes(query);
            });
        });

        select.addEventListener("change", () => {
            input.value = "";
            searchableOptions(select).forEach(option => option.hidden = false);
        });

        sortOptions(select);
        updateSearchVisibility(select, searchContainer);

        const observer = new MutationObserver(() => {
            sortOptions(select);
            updateSearchVisibility(select, searchContainer);
        });
        observer.observe(select, { childList: true, subtree: true });
    }

    function initialize(root) {
        if (root.matches?.("select")) {
            addSearch(root);
        }
        root.querySelectorAll?.("select").forEach(addSearch);
    }

    document.addEventListener("DOMContentLoaded", () => {
        initialize(document);
        new MutationObserver(mutations => mutations.forEach(mutation =>
            mutation.addedNodes.forEach(node => {
                if (node.nodeType === Node.ELEMENT_NODE) {
                    initialize(node);
                }
            })))
            .observe(document.body, { childList: true, subtree: true });
    });
})();
