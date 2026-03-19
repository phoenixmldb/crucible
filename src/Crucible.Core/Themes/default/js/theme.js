(function () {
  "use strict";
  var toggle = document.querySelector(".nav-toggle");
  var sidebar = document.querySelector(".sidebar");
  if (toggle && sidebar) {
    toggle.addEventListener("click", function () {
      sidebar.classList.toggle("open");
      var expanded = sidebar.classList.contains("open");
      toggle.setAttribute("aria-expanded", String(expanded));
    });
  }
})();
