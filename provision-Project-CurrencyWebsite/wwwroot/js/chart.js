$(document).ready(function () {
    $("#searchBtn").click(function () {
        let currency = $("#currencyCode").val().trim();

        if (!currency) {
            alert("Please enter a currency code.");
            return;
        }

        $.ajax({
            url: `http://localhost:5002/api/exchangeRates/${currency}`,
            method: "GET",
            headers: { "Accept": "application/json" }, // Request JSON explicitly
            dataType: "json", // Expect JSON response
            success: function (data) {
                console.log("Received data:", data);

                if (data.length === 0) {
                    alert("No data found for this currency.");
                    return;
                }
                generateChart(data);
            },
            error: function (xhr) {
                console.error("AJAX Error:", xhr);
                if (xhr.status === 404) {
                    alert("Currency not found.");
                } else {
                    alert("Error retrieving data.");
                }
            }
        });



        function generateChart(data) {
            $("#chart").empty(); // Clear previous chart

            let width = 600, height = 400, margin = { top: 20, right: 40, bottom: 50, left: 50 };

            let svg = d3.select("#chart")
                .append("svg")
                .attr("width", width)
                .attr("height", height)
                .append("g")
                .attr("transform", `translate(${margin.left},${margin.top})`);

            // Convert date strings to Date objects
            let parseDate = d3.timeParse("%Y-%m-%dT%H:%M:%S");
            data.forEach(d => d.date = parseDate(d.date));

            // Sort data by date
            data.sort((a, b) => a.date - b.date);

            // Calculate dynamic Y-axis range with smaller margins
            let minY = d3.min(data, d => d.forexBuying);
            let maxY = d3.max(data, d => d.forexBuying);

            let yMin = minY - (minY * 0.01); // 10% below minimum
            let yMax = maxY + (maxY * 0.01); // 10% above maximum

            // Define scales
            let xScale = d3.scaleTime()
                .domain(d3.extent(data, d => d.date))
                .range([0, width - margin.left - margin.right]);

            let yScale = d3.scaleLinear()
                .domain([yMin, yMax])
                .nice()
                .range([height - margin.top - margin.bottom, 0]);

            // Define axes
            let xAxis = d3.axisBottom(xScale).tickFormat(d3.timeFormat("%Y-%m"));

            let yAxis = d3.axisLeft(yScale)
                .ticks(7)
                .tickFormat(d => (d % 1 === 0 ? d : d3.format(".2f")(d))); // No .00, keeps precision if needed

            // Add grid lines
            svg.append("g")
                .attr("class", "grid")
                .call(d3.axisLeft(yScale).tickSize(-width + margin.left + margin.right).tickFormat(""));

            svg.append("g")
                .attr("class", "grid")
                .attr("transform", `translate(0, ${height - margin.top - margin.bottom})`)
                .call(d3.axisBottom(xScale).tickSize(-height + margin.top + margin.bottom).tickFormat(""));

            // Add X Axis
            svg.append("g")
                .attr("transform", `translate(0, ${height - margin.top - margin.bottom})`)
                .call(xAxis)
                .selectAll("text")
                .attr("transform", "rotate(-45)")
                .style("text-anchor", "end");

            // Add Y Axis
            svg.append("g").call(yAxis);

            // Define line generator
            let line = d3.line()
                .x(d => xScale(d.date))
                .y(d => yScale(d.forexBuying))
                .curve(d3.curveMonotoneX);

            // Add the line
            svg.append("path")
                .datum(data)
                .attr("fill", "none")
                .attr("stroke", "steelblue")
                .attr("stroke-width", 2)
                .attr("d", line);

            // Add circles at data points
            svg.selectAll("circle")
                .data(data)
                .enter()
                .append("circle")
                .attr("cx", d => xScale(d.date))
                .attr("cy", d => yScale(d.forexBuying))
                .attr("r", 3)
                .attr("fill", "red");

            // Tooltip
            let tooltip = d3.select("#chart").append("div")
                .attr("class", "tooltip")
                .style("position", "absolute")
                .style("background", "#fff")
                .style("border", "1px solid #000")
                .style("padding", "5px")
                .style("display", "none");

            svg.selectAll("circle")
                .on("mouseover", function (event, d) {
                    tooltip.style("display", "block")
                        .html(`Date: ${d3.timeFormat("%Y-%m-%d")(d.date)}<br>Rate: ${d.forexBuying}`)
                        .style("left", (event.pageX + 10) + "px")
                        .style("top", (event.pageY - 20) + "px");
                })
                .on("mouseout", () => tooltip.style("display", "none"));
        }
    });
});

