// Home | Clan Mains script

var ratingColors = ["#930D0D", "#CD3333", "#CC7A00", "#CCB800", "#839A24", "#4C7226", "#4098BE", "#376DBD", "#793DB6", "#401070"];

// Faz o histograma de distribuição por Rating
// graphType pode ser "player" ou "battle"
function doRatingGraph(graphType) {

    var decimalSeparator = $("body").data("requestDecimalSeparator");
    var groupingSeparator = $("body").data("requestGroupingSeparator");

    var frequencyColumn = 2;
    var axisTitle = $("#ratingDistribuctionByPlayer").data("axisLabel");
    if (graphType === "battle") {
        frequencyColumn = 4;
        axisTitle = $("#ratingDistribuctionByBattle").data("axisLabel");;
    }

    // Extrai da tabela de Rating os dados
    var ratings = $("#RatingDistibuctionTable tbody tr td:nth-child(1)").map(function () {
        var a = $(this).text();
        return a;
    }).get();

    var selector = "#RatingDistibuctionTable tbody tr td:nth-child(" + frequencyColumn + ")";
    var frequencies = $(selector).map(function () {
        var a = $(this).text();
        a = a.replace(groupingSeparator, "");
        a = a.replace(decimalSeparator, ".");
        var d = parseFloat(a);
        return d;
    }).get();

    var ratingFrequencies = [];
    var i;
    for (i = 0; i < ratings.length; i++) {
        ratingFrequencies.push([ratings[i], frequencies[i]]);
    }

    $("#ratingDistribuctionContainer").highcharts({
        chart: {
            type: "column"
        },
        title: {
            text: null
        },
        xAxis: {
            type: "category"
        },
        yAxis: {
            title: {
                text: axisTitle
            },
            allowDecimals: false
        },
        series: [
            {
                name: axisTitle,
                data: ratingFrequencies
            }
        ],
        plotOptions: {
            column: {
                pointPadding: 0,
                borderWidth: 0,
                groupPadding: 0,
                shadow: true,
                colorByPoint: true,
                colors: ratingColors
            }
        },
        legend: {
            enabled: false
        }
    });

}

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

    // Troca a hora UTC de atualização para a hora correspondente no cliente
    $("#last-update-time").text(convertToLocalTime($("#last-update-time").text()));

    // Troca a hora UTC nos titulos
    $(".title-moment").each(function () {
        var item = $(this);

        var re = /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)*Z/g;

        function replacer(match) {
            return convertToLocalTime(match);
        }

        var currentTitle = item.attr("title");
        var newTitle = currentTitle.replace(re, replacer);
        item.attr("title", newTitle);

    });

    var decimalSeparator = $("body").data("requestDecimalSeparator");
    var groupingSeparator = $("body").data("requestGroupingSeparator");

    // Função de ordenção numerica do lado do cliente
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

    $("#membersTable").DataTable({
        "paging": true,
        "lengthChange": false,
        "pageLength": 15,
        "pagingType": "numbers",
        "info": false,
        "searching": false,
        "columnDefs": [
            { "type": "numeric-flex", targets: [2, 3, 4, 5, 6, 7, 8, 9, 10] }
        ],
        "order": [[0, "asc"]]
    });

    $("#historicTable").DataTable({
        "paging": false,
        "info": false,
        "searching": false,
        "columnDefs": [
            { "type": "numeric-flex", targets: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11] },
            { "type": "date-iso", targets: [0] },
            { "orderable": false, targets: [] }
        ],
        "order": [[0, "desc"]]
    });

    var oAcesTable = $("#acesTable").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        searching: false,
        columnDefs: [
            { type: "numeric-flex", targets: [5, 6, 7, 8, 9, 10, 11] },
            { orderable: false, targets: [] },
            { searchable: false, targets: [5, 6, 7, 8, 9, 10, 11] }
        ],
        order: [[5, "asc"]],
        dom: "tp",
        language: {
            paginate: {
                previous: "@Resources.Previous",
                next: "@Resources.Next"
            }
        }
    });

    doRatingGraph("player");

    $("#ratingDistribuctionByPlayer").click(function () {
        doRatingGraph("player");
        return false;
    });

    $("#ratingDistribuctionByBattle").click(function () {
        doRatingGraph("battle");
        return false;
    });

    $("#historicGraph").hide();

    // extrai da tabela de historico os dados para o grafico

    var dates = $("#historicTable tbody tr td:nth-child(1)").map(function () {
        var a = $(this).text();
        if (a.substring(0, 3) === "<a ") {
            a = a.match(/<a [^>]+>([^<]+)<\/a>/)[1];
        }
        var brDate = a.split("-");
        var dt = Date.UTC(brDate[0] * 1, brDate[1] * 1 - 1, brDate[2] * 1);
        return dt;
    }).get();

    var wn8As = $("#historicTable tbody tr td:nth-child(6)").map(function () {
        var a = $(this).text();
        a = a.replace(groupingSeparator, "");
        a = a.replace(decimalSeparator, ".");
        var d = parseFloat(a);
        return d;
    }).get();

    var wn8T15S = $("#historicTable tbody tr td:nth-child(7)").map(function () {
        var a = $(this).text();
        a = a.replace(groupingSeparator, "");
        a = a.replace(decimalSeparator, ".");
        var d = parseFloat(a);
        return d;
    }).get();

    var wn8T7S = $("#historicTable tbody tr td:nth-child(8)").map(function () {
        var a = $(this).text();
        a = a.replace(groupingSeparator, "");
        a = a.replace(decimalSeparator, ".");
        var d = parseFloat(a);
        return d;
    }).get();

    var wn8S = $("#historicTable tbody tr td:nth-child(12)").map(function () {
        var a = $(this).text();
        a = a.replace(groupingSeparator, "");
        a = a.replace(decimalSeparator, ".");
        var d = parseFloat(a);
        return d;
    }).get();

    var dateWn8As = [];
    var dateWn8T15S = [];
    var dateWn8T7S = [];
    var dateWn8S = [];
    for (var i = 0; i < dates.length; i++) {
        dateWn8As.push([dates[i], wn8As[i]]);
        dateWn8T15S.push([dates[i], wn8T15S[i]]);
        dateWn8T7S.push([dates[i], wn8T7S[i]]);
        dateWn8S.push([dates[i], wn8S[i]]);
    }

    dateWn8As.reverse();
    dateWn8T15S.reverse();
    dateWn8T7S.reverse();
    dateWn8S.reverse();

    var bandNames = $("#historicContainer").data("bandNames").split(";");
    var axisTitle = $("#historicContainer").data("axisTitle");

    if (dateWn8As.length > 1) {
        $("#historicGraph").show();


        // faz o grafico de historico
        $("#historicContainer").highcharts({
            chart: {
                type: "spline"
            },
            title: {
                text: null
            },
            xAxis: {
                type: "datetime",
                labels: {
                    overflow: "justify",
                    format: "{value: %m-%d}"
                }
            },
            yAxis: {
                title: {
                    text: axisTitle
                },
                minorGridLineWidth: 0,
                gridLineWidth: 0,
                alternateGridColor: null,
                plotBands: [
                    {
                        // very bad
                        from: 0.0,
                        to: 300.0,
                        color: ratingColors[0],
                        label: {
                            text: bandNames[0],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }, {
                        // Ruim
                        from: 300.0,
                        to: 450.0,
                        color: ratingColors[1],
                        label: {
                            text: bandNames[1],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }, {
                        // Abaixo da Media
                        from: 450.0,
                        to: 650.0,
                        color: ratingColors[2],
                        label: {
                            text: bandNames[2],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }, {
                        // Médio
                        from: 650.0,
                        to: 900.0,
                        color: ratingColors[3],
                        label: {
                            text: bandNames[3],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }, {
                        // Acima da Media
                        from: 900.0,
                        to: 1200.0,
                        color: ratingColors[4],
                        label: {
                            text: bandNames[4],
                            style: {
                                color: "#B0B0B0"
                            }
                        }
                    }, {
                        // Bom
                        from: 1200.0,
                        to: 1600.0,
                        color: ratingColors[5],
                        label: {
                            text: bandNames[5],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }, {
                        // Muito Bom
                        from: 1600.0,
                        to: 2000.0,
                        color: ratingColors[6],
                        label: {
                            text: bandNames[6],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }, {
                        // Excelente
                        from: 2000.0,
                        to: 2450.0,
                        color: ratingColors[7],
                        label: {
                            text: bandNames[7],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }, {
                        // Unicum
                        from: 2450.0,
                        to: 2900.0,
                        color: ratingColors[8],
                        label: {
                            text: bandNames[8],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }, {
                        // Super Unicum
                        from: 2900.0,
                        to: 8000.0,
                        color: ratingColors[9],
                        label: {
                            text: bandNames[9],
                            style: {
                                color: "#A0A0A0"
                            }
                        }
                    }
                ]
            },
            plotOptions: {
                spline: {
                    lineWidth: 2,
                    states: {
                        hover: {
                            lineWidth: 5
                        }
                    },
                    marker: {
                        enabled: false
                    }
                }
            },
            series: [
                {
                    name: "WN8a",
                    data: dateWn8As

                },
                {
                    name: "WN8t15",
                    data: dateWn8T15S
                },
                {
                    name: "WN8t7",
                    data: dateWn8T7S
                },
                {
                    name: "WN8",
                    data: dateWn8S
                }
            ],
            navigation: {
                menuItemStyle: {
                    fontSize: "10px"
                }
            }
        });
    }



});
