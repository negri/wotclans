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


    const apiUrl = $("#clanTable").data("apiUrl");

    // to retrieve translations from te page
    const translationUpdatedAt = $("#clanTable").data("translationUpdatedAt");


    // Configura a tabela de dados
    const oTable = $("#clanTable").DataTable({
        paging: true,
        lengthChange: false,
        pageLength: 25,
        pagingType: "numbers",
        info: false,
        searching: true,
        processing: true,
        serverSide: true,

        ajax: {
            url: apiUrl,
            type: "POST"
        },

        stateSave: true,
        stateDuration: -1,
        stateLoaded: function (settings, data) {
            //var filterString = data.search.search;
            //if (filterString !== "") {
            //    $("#searchBox").val(filterString);
            //}
        },
        
        columnDefs: [
            { searchable: false, targets: [0, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12] }
        ],

        columns: [
            {
                data: "Rank",
                className: "number"
            },
            {
                data: "ClanTag",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const tag = a[0];
                    const flag = a[1];

                    var isOldDataClass = "";
                    if (a[2] === "True") {
                        isOldDataClass = " class=\"old-data\"";
                    }

                    const name = a[3].replace("|ç|", ";");

                    var s = `<a href="./Clan/${tag}"${isOldDataClass} title="${name}">${tag}`;
                    if (a[1] !== "") {
                        const flagImg = `<img src="./Images/Flags/${flag}.png" alt="${flag.toUpperCase()}" title="${flag.toUpperCase()}" />`;
                        s = s + flagImg;
                    }
                    s = s + "</a>";
                    return s;
                }
            },
            {
                data: "Composition",
                render: function (data, type, full, meta) {
                    const a = data.split(";");

                    const [psIndex, psCount, xboxCount] = a;

                    const s = `<progress class="platform-mix" max="100" value="${psIndex}" title="${psCount} on PS; ${xboxCount} on XBOX"></progress>`;
                    
                    return s;
                }
            },
            {
                data: "Active",
                className: "number"
            },
            {
                data: "ActiveBattles",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [battles, utcMoment] = a;
                    const localMoment = convertToLocalTime(utcMoment);

                    const s = `<span title="${translationUpdatedAt} ${localMoment}">${battles}</span>`;

                    return s;
                }
            },
            {
                data: "ActiveWinRate",
                className: "number"
            },
            {
                data: "ActiveWn8",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [wn8, labelClass, rating, color] = a;
                    const s = `<div title="${rating}" style="background-color: ${color}" class="${labelClass} bleed-right">${wn8}</div>`;
                    return s;
                }
            },
            {
                data: "Top15Wn8",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [wn8, labelClass, rating, color] = a;
                    const s = `<div title="${rating}" style="background-color: ${color}" class="${labelClass} bleed-right">${wn8}</div>`;
                    return s;
                }
            },
            {
                data: "Top7Wn8",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [wn8, labelClass, rating, color] = a;
                    const s = `<div title="${rating}" style="background-color: ${color}" class="${labelClass}">${wn8}</div>`;
                    return s;
                }
            },
            {
                data: "Count",
                className: "number"
            },
            {
                data: "TotalBattles",
                className: "number"
            },
            {
                data: "TotalWinRate",
                className: "number"
            },
            {
                data: "TotalWn8",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [wn8, labelClass, rating, color] = a;
                    const s = `<div title="${rating}" style="background-color: ${color}" class="${labelClass}">${wn8}</div>`;
                    return s;
                }
            }
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
        //oTable.search($(this).val()).draw();
    });

    // Troca a hora UTC de atualização para a hora correspondente no cliente
    $("#last-update-time").text(convertToLocalTime($("#last-update-time").text()));

    // Troca a hora UTC nos titulos
    //$(".title-moment").each(function (index) {
    //    var item = $(this);

    //    var re = /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)*Z/g;

    //    function replacer(match) {
    //        return convertToLocalTime(match);
    //    }

    //    var currentTitle = item.attr("title");
    //    var newTitle = currentTitle.replace(re, replacer);
    //    item.attr("title", newTitle);

    //});


});


