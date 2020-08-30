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

$(document).ready(function() {

    // Convert time of last update to local time
    $("#last-update-time").text(convertToLocalTime($("#last-update-time").text()));

    // Where the grid API is
    const apiUrl = $("#clanTable").data("apiUrl");

    // to retrieve translations from te page
    const translationUpdatedAt = $("#clanTable").data("translationUpdatedAt");


    // Configure the data table
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
        stateDuration: 60 * 60 *  24 * 7,
        stateLoaded: function (settings, data) {

            // nation and filter
            const nationAndFilter = data.columns[1].search.search;
            if (nationAndFilter !== "") {
                const a = nationAndFilter.split(";");
                const [clan, nation] = a;

                if (clan !== "") {
                    $("#searchBox").val(clan);
                }

                if (nation !== "") {
                    $(`#btnNation-${nation.toLowerCase()}`).addClass("filter-button-selected");
                }
            }

            // actives
            const activesFilter = data.columns[7].search.search;
            if (activesFilter === "all") {
                $("#FilterAll").addClass("filter-button-selected");
            }
            else if (activesFilter === "big") {
                $("#FilterBig").addClass("filter-button-selected");
            }
            else if (activesFilter === "small") {
                $("#FilterSmall").addClass("filter-button-selected");
            }

        },

        columnDefs: [
            { searchable: false, targets: [0, 2, 4, 5, 6, 8, 9, 10, 11, 12] }
        ],

        columns: [
            {
                data: "Rank",
                className: "number"
            },
            {
                data: "ClanTag",
                render: function(data, type, full, meta) {
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
                        const flagImg =
                            `<img src="./Images/Flags/${flag}.png" alt="${flag.toUpperCase()}" title="${
                                flag.toUpperCase()}" />`;
                        s = s + flagImg;
                    }
                    s = s + "</a>";
                    return s;
                }
            },
            {
                data: "ActiveWn8",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [wn8, labelClass, rating, color] = a;
                    const s =
                        `<div title="${rating}" style="background-color: ${color}" class="${labelClass} bleed-right">${
                            wn8}</div>`;
                    return s;
                }
            },
            {
                data: "Top15Wn8",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [wn8, labelClass, rating, color] = a;
                    const s =
                        `<div title="${rating}" style="background-color: ${color}" class="${labelClass} bleed-right">${
                            wn8}</div>`;
                    return s;
                }
            },
            {
                data: "Top7Wn8",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [wn8, labelClass, rating, color] = a;
                    const s =
                        `<div title="${rating}" style="background-color: ${color}" class="${labelClass}">${wn8}</div>`;
                    return s;
                }
            },
            {
                data: "ActiveWinRate",
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
                data: "Active",
                className: "number"
            },
            {
                data: "TotalWn8",
                className: "number",
                render: function (data, type, full, meta) {
                    const a = data.split(";");
                    const [wn8, labelClass, rating, color] = a;
                    const s =
                        `<div title="${rating}" style="background-color: ${color}" class="${labelClass}">${wn8}</div>`;
                    return s;
                }
            },
            {
                data: "TotalWinRate",
                className: "number"
            },
            {
                data: "TotalBattles",
                className: "number"
            },
            {
                data: "Count",
                className: "number"
            },
            {
                data: "Composition",
                render: function(data, type, full, meta) {
                    const a = data.split(";");

                    const [psIndex, psCount, xboxCount] = a;

                    const s =
                        `<progress class="platform-mix" max="100" value="${psIndex}" title="${psCount} on PS; ${
                            xboxCount} on XBOX"></progress>`;

                    return s;
                }
            }
        ],

        order: [[2, "desc"]],
        dom: "lrtip",
        language: {
            processing: "⌛ Please wait..."
        }
    });

    // reset
    $("#resetFilters").click(function() {
        $("#searchBox").val("");
        $("#nationButtons .btn-nation").removeClass("filter-button-selected");
        $("#nationButtons").data("selectedNation", "");
        $("#activesButtons .btn-actives").removeClass("filter-button-selected");

        oTable.columns(1).search("");
        oTable.columns(7).search("").draw();
    });

    // nations buttons
    $("#nationButtons .btn-nation").click(function () {
        const btn = $(this);

        const nation = btn.data("nation");
        const clan = $("#searchBox").val();
        const search = `${clan};${nation}`;

        oTable.columns(1).search(search).draw();

        $("#nationButtons .btn-nation").removeClass("filter-button-selected");
        btn.addClass("filter-button-selected");
        $("#nationButtons").data("selectedNation", nation);
    });

    // clan search
    $("#searchBox").keyup(function () {
        var clan = $(this).val();

        var nation = "";
        if (clan.startsWith(";") === true) {
            nation = clan.split(";")[1];
            clan = "";
        } else {
            nation = $("#nationButtons").data("selectedNation");
        }

        const search = `${clan};${nation}`;

        oTable.columns(1).search(search).draw();
    });

    // Size and WN8t15 filters
    $("#activesButtons .btn-actives").click(function () {
        const btn = $(this);

        const type = btn.data("type");

        oTable.columns(7).search(type).draw();

        $("#activesButtons .btn-actives").removeClass("filter-button-selected");
        btn.addClass("filter-button-selected");
    });
    


});


