$(document).ready(function () {
    $("#searchBtn").click(function () {
        let currency = $("#currencyCode").val().trim();

        if (!currency) {
            alert("Please enter a currency code.");
            return;
        }

        $.ajax({
            url: `http://localhost:5002/api/BusinessApi/${currency}`,
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

            // Get exact min and max values for y-axis
            let minY = d3.min(data, d => d.forexBuying);
            let maxY = d3.max(data, d => d.forexBuying);

            // Define scales
            let xScale = d3.scaleTime()
                .domain(d3.extent(data, d => d.date))
                .range([0, width - margin.left - margin.right]);

            let yScale = d3.scaleLinear()
                .domain([minY, maxY]) // Use exact min and max
                .nice()
                .range([height - margin.top - margin.bottom, 0]);

            // Define axes
            let xAxis = d3.axisBottom(xScale).tickFormat(d3.timeFormat("%Y-%m"));

            let yAxis = d3.axisLeft(yScale)
                .ticks(10) // Force 10 ticks
                .tickFormat(d => (d % 1 === 0 ? d : d3.format(".2f")(d))); // Keep precision if needed

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
        }

    });
});

