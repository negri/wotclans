namespace Negri.Wot.Diagnostics
{
    /// <summary>
    /// Informação do uso de Memória pelo processo
    /// </summary>
    public class ProcessMemoryUsage
    {
        /// <summary>
        /// Total de Memória (mapeada ou não) do Processo
        /// </summary>
        public long Virtual { get; set; }

        /// <summary>
        /// Working set
        /// </summary>
        /// <remarks>
        /// From http://msdn.microsoft.com/en-us/library/windows/desktop/cc441804(v=vs.85).aspx
        /// The working set of a process is the set of pages in the virtual address space of the process that are currently resident in physical memory. 
        /// The working set contains only pageable memory allocations; 
        /// nonpageable memory allocations such as Address Windowing Extensions (AWE) or large page allocations are not included in the working set.
        /// </remarks>
        public long WorkingSet { get; set; }

        /// <summary>
        /// Working set em kB
        /// </summary>
        public long WorkingSetkB { get { return WorkingSet / 1024; } }

        /// <summary>
        /// Memória Privada
        /// </summary>
        public long Private { get; set; }

        /// <summary>
        /// Memoria Privada em kB
        /// </summary>
        public long PrivatekB { get { return Private / 1024; } }

        /// <summary>
        /// Memoria paginada do processo
        /// </summary>
        public long Paged { get; set; }

        /// <summary>
        /// Memoria paginada do processo, em kB
        /// </summary>
        public long PagedkB { get { return Paged / 1024; } }

        /// <summary>
        /// Não-paginada, fisica, alocada ao processo
        /// </summary>
        public long NonpagedSystem { get; set; }

        /// <summary>
        /// Não-paginada, fisica, alocada ao processo, em kB
        /// </summary>
        public long NonpagedSystemkB { get { return NonpagedSystem / 1024; } }

        /// <summary>
        /// Total paginável usável pelo processo
        /// </summary>
        public long PagedSystem { get; set; }

        /// <summary>
        /// Total paginável usável pelo processo, en kB
        /// </summary>
        public long PagedSystemkB { get { return PagedSystem / 1024; } }

        /// <summary>
        /// Numero de Threads do Process
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Numero de Módulos associados ao processo
        /// </summary>
        public int ModuleCount { get; set; }

        /// <summary>
        /// Numero de Handles associados ao processo
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// Memoria usada em MB
        /// </summary>
        public long WorkingSetMB { get { return WorkingSetkB/1024; } }
    }
}