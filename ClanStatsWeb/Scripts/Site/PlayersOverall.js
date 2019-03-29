// Players | Overall main script

// Converte hora UTC para hora local no formato do request
function convertToLocalTime(utcString) {
    var moment = window.moment.utc(utcString);
    var localOffset = window.moment().utcOffset();
    moment.add("minutes", localOffset);

    var format = $("body").data("requestDateFormat") + " " + $("body").data("requestTimeFormat");

    // normalização para o formato do moment
    format = format.replace(/d/g, "D").replace(/y/g, "Y").replace("tt", "a").replace("TT", "A");

    var s = moment.format(format);
    return s;
}

$(document).ready(function () {

    // Troca a hora UTC no texto
    $(".universal-time-text").each(function () {
        var item = $(this);

        var re = /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)*Z/g;

        function replacer(match) {
            return convertToLocalTime(match);
        }

        var currentTitle = item.text();
        var newTitle = currentTitle.replace(re, replacer);
        item.text(newTitle);

    });

    var decimalSeparator = $("body").data("requestDecimalSeparator");
    var groupingSeparator = $("body").data("requestGroupingSeparator");

    // Função de ordenação numérica do lado do cliente
    jQuery.extend(jQuery.fn.dataTableExt.oSort, {
        "numeric-flex-pre": function (a) {
            if (a === "") {
                return 0.0;
            }
            if (a === "-") {
                return 0.0;
            }

            if (a.substring(0, 3) === "<a ") {
                a = a.match(/<a [^>]+>([^<]+)<\/a>/)[1];
            }

            a = a.replace(groupingSeparator, "");
            a = a.replace("%", "");
            a = a.replace(decimalSeparator, ".");

            return parseFloat(a);
        },

        "numeric-flex-asc": function (a, b) {
            return ((a < b) ? -1 : ((a > b) ? 1 : 0));
        },

        "numeric-flex-desc": function (a, b) {
            return ((a < b) ? 1 : ((a > b) ? -1 : 0));
        }
    });

    jQuery.extend(jQuery.fn.dataTableExt.oSort, {
        "date-iso-pre": function (a) {

            if (a.substring(0, 3) === "<a ") {
                a = a.match(/<a [^>]+>([^<]+)<\/a>/)[1];
            }

            var brDate = a.split("-");
            return (brDate[0] + brDate[1] + brDate[2]) * 1;
        },

        "date-iso-asc": function (a, b) {
            return ((a < b) ? -1 : ((a > b) ? 1 : 0));
        },

        "date-iso-desc": function (a, b) {
            return ((a < b) ? 1 : ((a > b) ? -1 : 0));
        }
    });

    var oOverallTanks = $("#overallTanks").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        searching: true,
        columnDefs: [
            { type: "numeric-flex", targets: [4, 5, 6, 7, 8, 9, 10, 11] },
            { orderable: false, targets: [] },
            { searchable: false, targets: [4, 6, 7, 8, 9, 10, 11] }
        ],
        order: [[4, "asc"]],
        dom: "tp",
        language: {
            paginate: {
                previous: "@Resources.Previous",
                next: "@Resources.Next"
            }
        }
    });

    // reset
    $("#resetFilters").click(function () {
        $("#searchBox").val("");
        $("#nationButtons .btn-nation").removeClass("filter-button-selected");
        $("#typesButtons .btn-type").removeClass("filter-button-selected");
        $("#tiersButtons .btn-tier").removeClass("filter-button-selected");
        $("#battlesButtons .btn-battles").removeClass("filter-button-selected");

        oOverallTanks.columns(0).search("");
        oOverallTanks.columns(1).search("");
        oOverallTanks.columns(2).search("");
        oOverallTanks.columns(5).search("");
        oOverallTanks.search("").draw();
    });

    // Caixa de Buscas
    $("#searchBox").keyup(function () {
        oOverallTanks.search($(this).val()).draw();
    });

    // Por batalas
    $(".btn-battles").click(function () {
        var btn = $(this);
        var battles = btn.data("battles");
        oOverallTanks.columns(5).search(battles).draw();
        $("#battlesButtons .btn-battles").removeClass("filter-button-selected");
        btn.addClass("filter-button-selected");
    });

    // Por nação
    $(".btn-nation").click(function () {
        var btn = $(this);
        var nation = btn.data("nation");
        oOverallTanks.columns(0).search(nation).draw();
        $("#nationButtons .btn-nation").removeClass("filter-button-selected");
        btn.addClass("filter-button-selected");
    });

    // Por tipo
    $(".btn-type").click(function () {
        var btn = $(this);
        var type = btn.data("type");
        oOverallTanks.columns(1).search(type).draw();
        $("#typesButtons .btn-type").removeClass("filter-button-selected");
        btn.addClass("filter-button-selected");
    });

    // Por Tier
    $(".btn-tier").click(function () {
        var btn = $(this);
        var type = btn.data("tier");
        oOverallTanks.columns(2).search(type).draw();
        $("#tiersButtons .btn-tier").removeClass("filter-button-selected");
        btn.addClass("filter-button-selected");
    });

    var oAcesTable = $("#acesTable").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        searching: false,
        columnDefs: [
            { type: "numeric-flex", targets: [4, 5, 6, 7, 8, 9, 10] },
            { orderable: false, targets: [] }
        ],
        order: [[4, "asc"]],
        dom: "tp",
        language: {
            paginate: {
                previous: "@Resources.Previous",
                next: "@Resources.Next"
            }
        }
    });

    var oLeaderIfPlayABattle = $("#leaderIfPlayABattle").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        searching: false,
        columnDefs: [
            { type: "numeric-flex", targets: [4, 5, 6, 7, 8, 9, 10] },
            { orderable: false, targets: [] }
        ],
        order: [[4, "asc"]],
        dom: "tp",
        language: {
            paginate: {
                previous: "@Resources.Previous",
                next: "@Resources.Next"
            }
        }
    });

    var oLeaderIfFewMoreGames = $("#leaderIfFewMoreGames").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        searching: false,
        columnDefs: [
            { type: "numeric-flex", targets: [4, 5, 6, 7, 8, 9, 10] },
            { orderable: false, targets: [] }
        ],
        order: [[4, "asc"]],
        dom: "tp",
        language: {
            paginate: {
                previous: "@Resources.Previous",
                next: "@Resources.Next"
            }
        }
    });

    var oLeaderIfFewMoreDamage = $("#leaderIfFewMoreDamage").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        searching: false,
        columnDefs: [
            { type: "numeric-flex", targets: [4, 5, 6, 7, 8, 9, 10] },
            { orderable: false, targets: [] }
        ],
        order: [[4, "asc"]],
        dom: "tp",
        language: {
            paginate: {
                previous: "@Resources.Previous",
                next: "@Resources.Next"
            }
        }
    });

    var oLeaderOnNextUpdate = $("#leaderOnNextUpdate").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        searching: false,
        columnDefs: [
            { type: "numeric-flex", targets: [4, 5, 6, 7, 8, 9, 10] },
            { orderable: false, targets: [] }
        ],
        order: [[4, "asc"]],
        dom: "tp",
        language: {
            paginate: {
                previous: "@Resources.Previous",
                next: "@Resources.Next"
            }
        }
    });

});




