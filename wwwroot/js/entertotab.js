function enterToTab(event) {
    if (event.keyCode === 13) {
        var form = event.target.form;
        var index = Array.prototype.indexOf.call(form, event.target);
        var nextElement = form.elements[index + 1];

        if (nextElement) {
            nextElement.focus();
            event.preventDefault();
        }
    }
}

function addEnterToTabEvent() {
    var inputElements = document.querySelectorAll('input, select, textarea');

    for (var i = 0; i < inputElements.length; i++) {
        inputElements[i].addEventListener('keydown', enterToTab);
    }
}

window.addEventListener('DOMContentLoaded', addEnterToTabEvent);
