// Home | Index mains script

// Converte hora UTC para hora local no formato do request
function convertToLocalTime(utcString) {
    var moment = window.moment.utc(utcString);
    var localOffset = window.moment().utcOffset();
    moment.add(localOffset, "minutes");

    var format = $("body").data("requestDateFormat") + " " + $("body").data("requestTimeFormat");

    // normalização para o formato do moment
    format = format.replace(/d/g, "D").replace(/y/g, "Y").replace("tt", "a").replace("TT", "A");

    var s = moment.format(format);
    return s;
}

$(document).ready(function () {

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
            a = a.replace(groupingSeparator, "");
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

    // Função de ordenação sem considerar traços nos nomes dos clãs
    jQuery.extend(jQuery.fn.dataTableExt.oSort, {
        "no-dash-string-pre": function (a) {
            if (a === "") {
                return "";
            }

            if (a.substring(0, 3) === "<a ") {
                a = a.match(/<a [^>]+>([^<]+)<\/a>/)[1];
            }

            a = a.replace("-", "");
            a = a.replace("-", "");
            a = a.replace("-", "");
            a = a.replace("-", "");
            a = a.replace("-", "");

            a = a.replace("_", "");
            a = a.replace("_", "");
            a = a.replace("_", "");
            a = a.replace("_", "");
            a = a.replace("_", "");

            if (a === "") {
                return "";
            }

            return a;
        },

        "no-dash-string-asc": function (a, b) {
            return ((a < b) ? -1 : ((a > b) ? 1 : 0));
        },

        "no-dash-string-desc": function (a, b) {
            return ((a < b) ? 1 : ((a > b) ? -1 : 0));
        }
    });

    // Configura a tabela de dados
    var oTable = $("#clanTable").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        stateSave: true,
        stateDuration: -1,
        stateLoaded: function (settings, data) {
            var filterString = data.search.search;
            if (filterString !== "") {
                $("#searchBox").val(filterString);
            }
        },
        searching: true,
        columnDefs: [
            { type: "numeric-flex", targets: [0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] },
            { type: "no-dash-string", targets: [1] },
            { orderable: false, targets: [] },
            { searchable: false, targets: [0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] }
        ],
        order: [[7, "desc"]],
        dom: "lrtip",
        language: {
            paginate: {
                previous: "@Resources.Previous",
                next: "@Resources.Next"
            }
        }
    });

    $("#searchBox").keyup(function () {
        oTable.search($(this).val()).draw();
    });

    // Troca a hora UTC de atualização para a hora correspondente no cliente
    $("#last-update-time").text(convertToLocalTime($("#last-update-time").text()));

    // Troca a hora UTC nos titulos
    $(".title-moment").each(function (index) {
        var item = $(this);

        var re = /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)*Z/g;

        function replacer(match) {
            return convertToLocalTime(match);
        }

        var currentTitle = item.attr("title");
        var newTitle = currentTitle.replace(re, replacer);
        item.attr("title", newTitle);

    });


});


